using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;

namespace Micro_VS.ArgumentArgs_es
{
    public class CompileArgs : EventArgs
    {
        public CompileArgs(IEnumerable<CompilerError> errors, IEnumerable<CompilerError> warnings, IEnumerable<string> output, string outputAssembly)
        {
            Errors = (errors ?? throw new ArgumentNullException(nameof(errors))).ToList();
            Warnings = (warnings ?? throw new ArgumentNullException(nameof(warnings))).ToList();
            Output = (output ?? throw new ArgumentNullException(nameof(output))).ToList();
            OutputAssembly = outputAssembly;
        }

        public IReadOnlyList<CompilerError> Errors { get; }
        public IReadOnlyList<CompilerError> Warnings { get; }
        public IReadOnlyList<string> Output { get; }
        public string OutputAssembly { get; }
        public bool Succeeded => Errors.Count == 0;
    }
}
