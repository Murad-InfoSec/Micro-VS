using Micro_VS.ArgumentArgs_es;

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

        void InitializeProjectTree(InitializeProjectTreeArgs args);
        void InitializeFile(FileArgs args);
        void ShowBuildStarted();
        void ShowCompileResults(CompileArgs args);
    }
}
