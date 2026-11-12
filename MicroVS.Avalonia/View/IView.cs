using Micro_VS.ArgumentArgs_es;
using Micro_VS.Completion;

namespace Micro_VS.View
{
    public interface IView
    {
        event EventHandler<ProjectArgs> CreateProject;
        event EventHandler<ProjectArgs> OpenProject;

        event EventHandler<FileArgs> OpenFile;
        event EventHandler<FileArgs> CreateFile;
        event EventHandler<FileArgs> SaveFile;

        event EventHandler Build;
        event EventHandler Run;

        /// <summary>Code completion service the editor queries while typing (set by the Presenter).</summary>
        ICodeCompletion CodeCompletion { get; set; }

        void InitializeProjectTree(InitializeProjectTreeArgs args);
        void InitializeFile(FileArgs args);
        void ShowBuildStarted();
        void ShowCompileResults(CompileArgs args);
    }
}
