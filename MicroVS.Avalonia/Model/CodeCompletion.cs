using System.IO;
using Micro_VS.Completion;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;
using MicroFile = Micro_VS.Model.ProjectInfo_micro_sln_.File;
using MicroProject = Micro_VS.Model.ProjectInfo_micro_sln_.Project;
using RoslynCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;

namespace Micro_VS.Model
{
    /// <summary>
    /// Roslyn-based code completion (the engine behind Visual Studio IntelliSense). It keeps an in-memory
    /// workspace mirroring the current project: every .cs file, with the editor's unsaved text where a
    /// file is open, compiled against the same .NET framework assemblies as <see cref="Model.Run"/>.
    /// </summary>
    internal sealed class CodeCompletion : ICodeCompletion
    {
        private readonly Func<MicroProject> getProject;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        // Guarded by gate.
        private AdhocWorkspace workspace;
        private Solution solution;
        private ProjectId projectId;
        private string projectFolder;
        private readonly Dictionary<string, DocumentId> documentIds = new Dictionary<string, DocumentId>(StringComparer.Ordinal);

        public CodeCompletion(Func<MicroProject> getProject)
        {
            this.getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        }

        public async Task<CompletionResult> GetCompletionsAsync(CompletionRequest request, CancellationToken cancellationToken = default)
        {
            Document document = await SyncAsync(request, cancellationToken).ConfigureAwait(false);
            if (document == null)
                return CompletionResult.Empty;

            CompletionService service = CompletionService.GetService(document);
            if (service == null)
                return CompletionResult.Empty;

            int position = Math.Clamp(request.Position, 0, request.Text.Length);
            CompletionTrigger trigger = request.TriggerCharacter is char c
                ? CompletionTrigger.CreateInsertionTrigger(c)
                : CompletionTrigger.Invoke;

            if (request.TriggerCharacter != null)
            {
                SourceText text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                if (!service.ShouldTriggerCompletion(text, position, trigger))
                    return CompletionResult.Empty;
            }

            CompletionList list = await service.GetCompletionsAsync(document, position, trigger, cancellationToken: cancellationToken)
                                               .ConfigureAwait(false);
            if (list == null || list.ItemsList.Count == 0)
                return CompletionResult.Empty;

            var entries = list.ItemsList
                .OrderBy(i => i.SortText, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.DisplayText, StringComparer.Ordinal)
                .Select(i => new CompletionEntry(
                    i.DisplayTextPrefix + i.DisplayText + i.DisplayTextSuffix,
                    i.FilterText,
                    i.InlineDescription,
                    KindOf(i),
                    i.Rules.MatchPriority > MatchPriority.Default,
                    new ItemState(document, i)))
                .ToList();

            return new CompletionResult(list.Span.Start, list.Span.Length, entries, list.SuggestionModeItem != null);
        }

        public async Task<string> GetDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry?.State is not ItemState state)
                return "";
            CompletionService service = CompletionService.GetService(state.Document);
            CompletionDescription description = service == null ? null
                : await service.GetDescriptionAsync(state.Document, state.Item, cancellationToken).ConfigureAwait(false);
            return description?.Text ?? "";
        }

        public async Task<CompletionEdit> GetEditAsync(CompletionEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry?.State is not ItemState state)
                return null;
            CompletionService service = CompletionService.GetService(state.Document);
            if (service == null)
                return null;

            CompletionChange change = await service.GetChangeAsync(state.Document, state.Item, commitCharacter: null, cancellationToken)
                                                   .ConfigureAwait(false);
            TextChange edit = change.TextChange;
            return new CompletionEdit(edit.Span.Start, edit.Span.Length, edit.NewText ?? "", change.NewPosition);
        }

        /// <summary>
        /// Loads Roslyn and the framework references in the background, so the first real completion
        /// request is fast.
        /// </summary>
        public void WarmUp()
        {
            MicroProject project = getProject();
            string firstFile = project?.ProjectFolder.Components.OfType<MicroFile>()
                                      .FirstOrDefault(f => IsCSharp(f.Path))?.Name;
            if (firstFile == null)
                return;

            Task.Run(async () =>
            {
                try
                {
                    await GetCompletionsAsync(new CompletionRequest(firstFile, ReadFile(project, firstFile), 0, null, null))
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Warm-up is best effort.
                }
            });
        }

        /// <summary>Brings the workspace in line with the project and the editor buffers; returns the requested document.</summary>
        private async Task<Document> SyncAsync(CompletionRequest request, CancellationToken cancellationToken)
        {
            MicroProject project = getProject();
            if (project == null)
                return null;

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (workspace == null || !string.Equals(projectFolder, project.ProjectFolder.Path, StringComparison.Ordinal))
                    CreateWorkspace(project);

                var files = project.ProjectFolder.Components.OfType<MicroFile>()
                                   .Where(f => IsCSharp(f.Path))
                                   .ToList();

                // Files removed from the project.
                foreach (string path in documentIds.Keys.Except(files.Select(f => f.Path)).ToList())
                {
                    solution = solution.RemoveDocument(documentIds[path]);
                    documentIds.Remove(path);
                }

                foreach (MicroFile file in files)
                {
                    string text = file.Name == request.FileName ? request.Text
                                : request.OpenDocuments.TryGetValue(file.Name, out string open) ? open
                                : ReadFile(project, file.Name);

                    if (documentIds.TryGetValue(file.Path, out DocumentId id))
                    {
                        // Only replace text that changed, so Roslyn can reuse its cached compilation.
                        Document existing = solution.GetDocument(id);
                        if (existing == null || !existing.TryGetText(out SourceText current) || current.ToString() != text)
                            solution = solution.WithDocumentText(id, SourceText.From(text));
                    }
                    else
                    {
                        id = DocumentId.CreateNewId(projectId, file.Name);
                        solution = solution.AddDocument(DocumentInfo.Create(
                            id, file.Name,
                            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text), VersionStamp.Create())),
                            filePath: file.Path));
                        documentIds[file.Path] = id;
                    }
                }

                MicroFile requested = files.FirstOrDefault(f => f.Name == request.FileName);
                return requested == null ? null : solution.GetDocument(documentIds[requested.Path]);
            }
            finally
            {
                gate.Release();
            }
        }

        private void CreateWorkspace(MicroProject project)
        {
            workspace?.Dispose();
            workspace = new AdhocWorkspace();
            documentIds.Clear();
            projectFolder = project.ProjectFolder.Path;
            projectId = ProjectId.CreateNewId(project.Name);

            solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(
                projectId, VersionStamp.Create(), project.Name, project.Name, LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.ConsoleApplication),
                parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
                metadataReferences: Model.FrameworkReferences.Value));
        }

        private static string ReadFile(MicroProject project, string fileName)
        {
            string path = project.ProjectFolder.Components.OfType<MicroFile>().FirstOrDefault(f => f.Name == fileName)?.Path;
            try
            {
                return path != null && System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "";
            }
            catch (IOException)
            {
                return "";
            }
        }

        private static bool IsCSharp(string path) =>
            path != null && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

        private static CompletionKind KindOf(RoslynCompletionItem item)
        {
            foreach (string tag in item.Tags)
            {
                switch (tag)
                {
                    case WellKnownTags.Keyword: return CompletionKind.Keyword;
                    case WellKnownTags.Snippet: return CompletionKind.Snippet;
                    case WellKnownTags.Namespace: return CompletionKind.Namespace;
                    case WellKnownTags.Class: return CompletionKind.Class;
                    case WellKnownTags.Structure: return CompletionKind.Struct;
                    case WellKnownTags.Interface: return CompletionKind.Interface;
                    case WellKnownTags.Enum: return CompletionKind.Enum;
                    case WellKnownTags.EnumMember: return CompletionKind.EnumMember;
                    case WellKnownTags.Delegate: return CompletionKind.Delegate;
                    case WellKnownTags.TypeParameter: return CompletionKind.TypeParameter;
                    case WellKnownTags.ExtensionMethod: return CompletionKind.ExtensionMethod;
                    case WellKnownTags.Method: return CompletionKind.Method;
                    case WellKnownTags.Property: return CompletionKind.Property;
                    case WellKnownTags.Field: return CompletionKind.Field;
                    case WellKnownTags.Event: return CompletionKind.Event;
                    case WellKnownTags.Constant: return CompletionKind.Constant;
                    case WellKnownTags.Local: return CompletionKind.Local;
                    case WellKnownTags.Parameter: return CompletionKind.Parameter;
                }
            }
            return CompletionKind.Other;
        }

        private sealed record ItemState(Document Document, RoslynCompletionItem Item);
    }
}
