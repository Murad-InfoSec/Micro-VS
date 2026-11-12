using Micro_VS.ArgumentArgs_es;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Micro_VS.View
{    
   public interface IView
    {
        event EventHandler<ProjectArgs> CreateProject;
        event EventHandler<ProjectArgs> OpenProject;

        event EventHandler<FileArgs> OpenFile;
        event EventHandler<FileArgs> CreateFile;
        event EventHandler<FileArgs> SaveFile;

        event EventHandler Run;

        void InitializeProjectTree(InitializeProjectTreeArgs args);
        void InitializeFile(FileArgs args);
        void ShowCompileResults(CompileArgs args);
    }
}
