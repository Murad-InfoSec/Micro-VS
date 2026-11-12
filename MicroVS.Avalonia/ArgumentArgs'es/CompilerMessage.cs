namespace Micro_VS.ArgumentArgs_es
{
    /// <summary>
    /// One compiler error or warning (replaces System.CodeDom.Compiler.CompilerError).
    /// Property order is the column order in the Errors / Warnings grids.
    /// </summary>
    public class CompilerMessage
    {
        public CompilerMessage(string code, string description, string filePath, int line, int column, bool isWarning)
        {
            Code = code ?? "";
            Description = description ?? "";
            FilePath = filePath ?? "";
            Line = line;
            Column = column;
            IsWarning = isWarning;
        }

        public string Code { get; }
        public string Description { get; }
        public string File => string.IsNullOrEmpty(FilePath) ? "" : System.IO.Path.GetFileName(FilePath);
        public int Line { get; }
        public int Column { get; }

        internal string FilePath { get; }
        internal bool IsWarning { get; }

        public override string ToString() =>
            (File.Length > 0 ? $"{File}({Line},{Column}): " : "") + (IsWarning ? "warning " : "error ") + Code + ": " + Description;
    }
}
