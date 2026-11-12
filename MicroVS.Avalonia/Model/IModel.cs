using Micro_VS.ArgumentArgs_es;
using Micro_VS.Completion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Micro_VS.Model
{
    public interface IModel
    {
        event EventHandler<InitializeProjectTreeArgs> ProjectCreatedOrOpened;
        event EventHandler<CompileArgs> ProjectCompilied;

        event EventHandler<FileArgs> FileOpenedOrCreated;

        void CreateProject(ProjectArgs args);
        void OpenProject(ProjectArgs args);
        void OpenFile(FileArgs args);
        void SaveFile(FileArgs args);
        void CreateFile(FileArgs args);
        ICodeCompletion CodeCompletion { get; }

        void Build();
        void Run();
    }
}
