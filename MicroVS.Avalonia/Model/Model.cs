using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml.Serialization;
using Micro_VS.ArgumentArgs_es;
using Micro_VS.Completion;
using Micro_VS.Model.ProjectInfo_micro_sln_;
using Project = Micro_VS.Model.ProjectInfo_micro_sln_.Project; // not Microsoft.CodeAnalysis.Project
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Micro_VS.Model
{
    public class Model : IModel
    {
        private const string ProgramCsTemplate =
@"using System;

namespace {0}
{{
    class Program
    {{
        static void Main(string[] args)
        {{
            Console.WriteLine(""Hello from Micro VS!"");
        }}
    }}
}}
";

        private readonly CodeCompletion completion;

        public Model()
        {
            completion = new CodeCompletion(() => CurrentProject);
        }

        public Project CurrentProject { get; set; }

        /// <summary>Roslyn code completion for the current project.</summary>
        public ICodeCompletion CodeCompletion => completion;

        /// <summary>When false, Run() only builds. Used by tests so no terminal window opens.</summary>
        public bool LaunchProgramAfterBuild { get; set; } = true;

        public event EventHandler<InitializeProjectTreeArgs> ProjectCreatedOrOpened;
        public event EventHandler<FileArgs> FileOpenedOrCreated;
        public event EventHandler<CompileArgs> ProjectCompilied;

        public void CreateProject(ProjectArgs args)
        {
            //Creates project folder .
            string ProjectDirPath = Directory.CreateDirectory(args.Path).FullName;

            //Determines micro_sln and program.cs files paths.
            string MicroSlnPath = Path.Combine(ProjectDirPath, args.Name + ".micro_sln");
            string ProgramCsPath = Path.Combine(ProjectDirPath, "Program.cs");

            //Creates micro_sln file and program.cs file .
            ProjectInfo_micro_sln_.File microSln = new ProjectInfo_micro_sln_.File(args.Name + ".micro_sln", MicroSlnPath);
            ProjectInfo_micro_sln_.File programCs = new ProjectInfo_micro_sln_.File("Program.cs", ProgramCsPath);
            if (!System.IO.File.Exists(ProgramCsPath))
                System.IO.File.WriteAllText(ProgramCsPath, string.Format(ProgramCsTemplate, ToIdentifier(args.Name)));

            //Creates project .
            CurrentProject = new Project(args.Name, microSln, programCs, ProjectDirPath);

            //Serializes Project in micro_sln file .
            SaveProjectSln();

            //Notice the Presenter the project is Created .
            ProjectCreatedOrOpened?.Invoke(this, new InitializeProjectTreeArgs(CurrentProject.Name, CurrentProject.ProjectFolder));
            completion.WarmUp();
        }

        public void OpenProject(ProjectArgs args)
        {
            using (FileStream fs = new FileStream(args.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                XmlSerializer xmlDeserializer = new XmlSerializer(typeof(Project));
                CurrentProject = (Project)xmlDeserializer.Deserialize(fs);
            }

            ProjectCreatedOrOpened?.Invoke(this, new InitializeProjectTreeArgs(CurrentProject.Name, CurrentProject.ProjectFolder));
            completion.WarmUp();
        }

        private void SaveProjectSln()
        {
            //Serializes Project in micro_sln file .
            using (FileStream micro_sln_file = new FileStream(CurrentProject.MicroSln.Path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Project));
                xmlSerializer.Serialize(micro_sln_file, CurrentProject);
            }
        }

        public void OpenFile(FileArgs args)
        {
            var file = CurrentProject?.ProjectFolder.Components.Find(a => a.Name == args._File.Name) as ProjectInfo_micro_sln_.File;
            if (file == null)
                return;
            args._File = file;
            args._File.Text = System.IO.File.ReadAllText(args._File.Path);
            FileOpenedOrCreated?.Invoke(this, args);
        }

        public void CreateFile(FileArgs args)
        {
            if (CurrentProject == null)
                return;
            if (!System.IO.File.Exists(args._File.Path))
                System.IO.File.WriteAllText(args._File.Path, "");
            if (!CurrentProject.ProjectFolder.Components.Any(a => a.Name == args._File.Name))
                CurrentProject.ProjectFolder.Components.Add(args._File);

            SaveProjectSln();
            ProjectCreatedOrOpened?.Invoke(this, new InitializeProjectTreeArgs(CurrentProject.Name, CurrentProject.ProjectFolder));
        }

        public void SaveFile(FileArgs args)
        {
            var fileToSave = CurrentProject?.ProjectFolder.Components.Find(a => a.Name == args._File.Name) as ProjectInfo_micro_sln_.File;
            if (fileToSave != null)
                SaveFile(fileToSave.Path, args._File.Text);
        }

        private void SaveFile(string path, string text)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.Write(text);
                }
            }
        }

        public void Build() => Compile(launch: false);

        public void Run() => Compile(launch: LaunchProgramAfterBuild);

        #region Compiling (Roslyn)

        // Every assembly of the running .NET shared framework (System.*, Microsoft.*, netstandard ...),
        // so compiled programs can use the whole base class library.
        internal static readonly Lazy<List<MetadataReference>> FrameworkReferences = new Lazy<List<MetadataReference>>(() =>
        {
            string frameworkDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var references = new List<MetadataReference>();
            foreach (string dll in Directory.GetFiles(frameworkDir, "*.dll"))
            {
                try
                {
                    using (var pe = new PEReader(System.IO.File.OpenRead(dll)))
                        if (!pe.HasMetadata) continue;
                    references.Add(MetadataReference.CreateFromFile(dll));
                }
                catch (BadImageFormatException) { }
            }
            return references;
        });

        private void Compile(bool launch)
        {
            if (CurrentProject == null)
                return;

            string outputDir = Path.Combine(CurrentProject.ProjectFolder.Path, "bin");
            string outputAssembly = Path.Combine(outputDir, CurrentProject.Name + ".dll");

            string[] fileNames = CurrentProject.ProjectFolder.Components
                .Where(a => a is ProjectInfo_micro_sln_.File && a.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Path)
                .ToArray();

            var output = new List<string>();
            var errors = new List<CompilerMessage>();
            var warnings = new List<CompilerMessage>();

            try
            {
                if (fileNames.Length == 0)
                {
                    errors.Add(new CompilerMessage("", "The project has no .cs files to compile.", null, 0, 0, false));
                }
                else
                {
                    EmitToDisk(fileNames, outputAssembly, errors, warnings);
                }
            }
            catch (Exception ex)
            {
                errors.Add(new CompilerMessage("", "Build failed: " + ex.Message, null, 0, 0, false));
            }

            output.AddRange(errors.Concat(warnings).Select(m => m.ToString()));

            if (errors.Count == 0)
            {
                output.Add("Output: " + outputAssembly);
                if (launch)
                {
                    try
                    {
                        StartProgram(outputAssembly);
                        output.Add("Started " + CurrentProject.Name + " in a new terminal window.");
                    }
                    catch (Exception ex)
                    {
                        output.Add("Build succeeded, but the program could not be started: " + ex.Message);
                    }
                }
            }

            ProjectCompilied?.Invoke(this, new CompileArgs(errors, warnings, output, outputAssembly));
        }

        private void EmitToDisk(string[] fileNames, string outputAssembly, List<CompilerMessage> errors, List<CompilerMessage> warnings)
        {
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
            var trees = fileNames.Select(f =>
                CSharpSyntaxTree.ParseText(SourceText.From(System.IO.File.ReadAllText(f), Encoding.UTF8), parseOptions, f));

            var compilation = CSharpCompilation.Create(
                CurrentProject.Name,
                trees,
                FrameworkReferences.Value,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication,
                                             optimizationLevel: OptimizationLevel.Release,
                                             warningLevel: 3));

            using (var pe = new MemoryStream())
            using (var pdb = new MemoryStream())
            {
                EmitResult result = compilation.Emit(pe, pdb,
                    options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

                foreach (Diagnostic d in result.Diagnostics)
                {
                    if (d.IsSuppressed || (d.Severity != DiagnosticSeverity.Error && d.Severity != DiagnosticSeverity.Warning))
                        continue;
                    FileLinePositionSpan span = d.Location.GetMappedLineSpan();
                    bool hasFile = d.Location.IsInSource;
                    var message = new CompilerMessage(d.Id, d.GetMessage(),
                        hasFile ? span.Path : null,
                        hasFile ? span.StartLinePosition.Line + 1 : 0,
                        hasFile ? span.StartLinePosition.Character + 1 : 0,
                        d.Severity == DiagnosticSeverity.Warning);
                    (message.IsWarning ? warnings : errors).Add(message);
                }

                if (!result.Success)
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(outputAssembly));
                System.IO.File.WriteAllBytes(outputAssembly, pe.ToArray());
                System.IO.File.WriteAllBytes(Path.ChangeExtension(outputAssembly, ".pdb"), pdb.ToArray());

                // Tells the dotnet host which runtime to run the program on (the same one Micro VS uses).
                Version v = Environment.Version;
                System.IO.File.WriteAllText(Path.ChangeExtension(outputAssembly, ".runtimeconfig.json"),
                    "{\n  \"runtimeOptions\": {\n    \"tfm\": \"net" + v.Major + "." + v.Minor + "\",\n" +
                    "    \"framework\": { \"name\": \"Microsoft.NETCore.App\", \"version\": \"" + v.Major + "." + v.Minor + ".0\" }\n  }\n}\n");
            }
        }

        #endregion

        #region Launching the compiled program

        /// <summary>Path of the dotnet host that runs compiled programs.</summary>
        public static string DotnetHostPath()
        {
            string exe = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
            if (Path.GetFileName(Environment.ProcessPath ?? "") == exe)
                return Environment.ProcessPath;

            // <dotnet root>/shared/Microsoft.NETCore.App/<version>/System.Private.CoreLib.dll
            string frameworkDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            string root = Path.GetFullPath(Path.Combine(frameworkDir, "..", "..", ".."));
            string candidate = Path.Combine(root, exe);
            return System.IO.File.Exists(candidate) ? candidate : exe;
        }

        // Like the original WinForms version, the program gets its own console window so it can
        // read input (Console.ReadLine) and stays open after it finishes.
        private static void StartProgram(string dllPath)
        {
            string dotnet = DotnetHostPath();
            string workDir = Path.GetDirectoryName(dllPath);

            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"\"{dotnet}\" \"{dllPath}\" & pause\"")
                {
                    UseShellExecute = true,
                    WorkingDirectory = workDir
                });
                return;
            }

            string script = Path.Combine(workDir, "run.command");
            System.IO.File.WriteAllText(script,
                "#!/bin/sh\n" +
                "cd " + ShellQuote(workDir) + "\n" +
                "clear\n" +
                ShellQuote(dotnet) + " " + ShellQuote(dllPath) + "\n" +
                "code=$?\n" +
                "echo\n" +
                "echo \"[Process exited with code $code. Press Enter to close.]\"\n" +
                "read _\n");
            System.IO.File.SetUnixFileMode(script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

            if (OperatingSystem.IsMacOS())
                Process.Start("open", new[] { "-a", "Terminal", script });
            else
                Process.Start("x-terminal-emulator", new[] { "-e", script });
        }

        private static string ShellQuote(string s) => "'" + s.Replace("'", "'\\''") + "'";

        #endregion

        private static string ToIdentifier(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            if (sb.Length == 0 || char.IsDigit(sb[0]))
                sb.Insert(0, '_');
            return sb.ToString();
        }
    }
}
