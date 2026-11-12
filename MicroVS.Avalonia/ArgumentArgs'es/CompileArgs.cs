namespace Micro_VS.ArgumentArgs_es
{
    public class CompileArgs : EventArgs
    {
        public CompileArgs(IEnumerable<CompilerMessage> errors, IEnumerable<CompilerMessage> warnings, IEnumerable<string> output, string outputAssembly)
        {
            Errors = (errors ?? throw new ArgumentNullException(nameof(errors))).ToList();
            Warnings = (warnings ?? throw new ArgumentNullException(nameof(warnings))).ToList();
            Output = (output ?? throw new ArgumentNullException(nameof(output))).ToList();
            OutputAssembly = outputAssembly;
        }

        public IReadOnlyList<CompilerMessage> Errors { get; }
        public IReadOnlyList<CompilerMessage> Warnings { get; }
        public IReadOnlyList<string> Output { get; }
        public string OutputAssembly { get; }
        public bool Succeeded => Errors.Count == 0;
    }
}
