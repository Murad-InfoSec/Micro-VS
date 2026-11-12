namespace Micro_VS.Completion
{
    /// <summary>
    /// Code completion for the files of the current project. Implemented by the Model with Roslyn;
    /// the View only sees this interface and the plain data types below.
    /// </summary>
    public interface ICodeCompletion
    {
        /// <summary>
        /// Completion items at <see cref="CompletionRequest.Position"/>. Returns <see cref="CompletionResult.Empty"/>
        /// when there is nothing to complete, or when a typed trigger character should not open the list.
        /// </summary>
        Task<CompletionResult> GetCompletionsAsync(CompletionRequest request, CancellationToken cancellationToken = default);

        /// <summary>Signature and documentation for an item (shown as a tooltip).</summary>
        Task<string> GetDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken = default);

        /// <summary>The text edit that inserts an item, in the coordinates of the request's text.</summary>
        Task<CompletionEdit> GetEditAsync(CompletionEntry entry, CancellationToken cancellationToken = default);
    }

    public sealed class CompletionRequest
    {
        public CompletionRequest(string fileName, string text, int position,
                                 IReadOnlyDictionary<string, string> openDocuments, char? triggerCharacter)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            Text = text ?? "";
            Position = position;
            OpenDocuments = openDocuments ?? new Dictionary<string, string>();
            TriggerCharacter = triggerCharacter;
        }

        /// <summary>Project file name, e.g. "Program.cs".</summary>
        public string FileName { get; }
        /// <summary>Current (possibly unsaved) text of that file.</summary>
        public string Text { get; }
        /// <summary>Caret offset in <see cref="Text"/>.</summary>
        public int Position { get; }
        /// <summary>Unsaved text of the other open files, by file name. Files not listed are read from disk.</summary>
        public IReadOnlyDictionary<string, string> OpenDocuments { get; }
        /// <summary>The character just typed, or null when completion was invoked explicitly (Ctrl+Space).</summary>
        public char? TriggerCharacter { get; }
    }

    public sealed class CompletionResult
    {
        public static readonly CompletionResult Empty = new CompletionResult(0, 0, Array.Empty<CompletionEntry>(), false);

        public CompletionResult(int spanStart, int spanLength, IReadOnlyList<CompletionEntry> items, bool isSuggestionMode)
        {
            SpanStart = spanStart;
            SpanLength = spanLength;
            Items = items;
            IsSuggestionMode = isSuggestionMode;
        }

        /// <summary>Start of the text the items replace (the partially typed word).</summary>
        public int SpanStart { get; }
        public int SpanLength { get; }
        /// <summary>Items in display order.</summary>
        public IReadOnlyList<CompletionEntry> Items { get; }
        /// <summary>
        /// True where the user is probably typing a new name (e.g. a variable declaration), so the list
        /// holds suggestions only and should not be committed automatically.
        /// </summary>
        public bool IsSuggestionMode { get; }
    }

    public sealed class CompletionEntry
    {
        public CompletionEntry(string displayText, string filterText, string inlineDescription,
                               CompletionKind kind, bool isPreselected, object state)
        {
            DisplayText = displayText ?? "";
            FilterText = string.IsNullOrEmpty(filterText) ? DisplayText : filterText;
            InlineDescription = inlineDescription ?? "";
            Kind = kind;
            IsPreselected = isPreselected;
            State = state;
        }

        /// <summary>Text shown in the list, e.g. "WriteLine" or "List&lt;&gt;".</summary>
        public string DisplayText { get; }
        /// <summary>Text matched against what the user types.</summary>
        public string FilterText { get; }
        /// <summary>Short gray hint shown after the item (may be empty).</summary>
        public string InlineDescription { get; }
        public CompletionKind Kind { get; }
        /// <summary>The item the list should select first (e.g. the expected type after "new").</summary>
        public bool IsPreselected { get; }
        /// <summary>Owned by the <see cref="ICodeCompletion"/> implementation.</summary>
        internal object State { get; }
    }

    /// <param name="Start">Offset where the replaced text starts.</param>
    /// <param name="Length">Length of the replaced text.</param>
    /// <param name="NewText">Text to insert.</param>
    /// <param name="NewCaret">Where the caret should go afterwards, or null to put it after <paramref name="NewText"/>.</param>
    public sealed record CompletionEdit(int Start, int Length, string NewText, int? NewCaret);

    public enum CompletionKind
    {
        Other, Keyword, Snippet, Namespace,
        Class, Struct, Interface, Enum, EnumMember, Delegate, TypeParameter,
        Method, ExtensionMethod, Property, Field, Event, Constant, Local, Parameter
    }
}
