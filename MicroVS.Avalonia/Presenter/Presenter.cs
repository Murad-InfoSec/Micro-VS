using Micro_VS.ArgumentArgs_es;
using Micro_VS.Model;
using Micro_VS.View;

namespace Micro_VS.Presenter
{
    public class Presenter
    {
        private IView view;
        private IModel model;

        public Presenter(IView view, IModel model)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.model = model ?? throw new ArgumentNullException(nameof(model));

            view.CreateProject += View_CreateEvent;
            view.OpenProject += View_OpenProject;
            view.OpenFile += View_OpenFile;
            view.CreateFile += View_CreateFile;
            view.SaveFile += View_SaveFile;
            view.Build += View_Build;
            view.Run += View_Run;
            view.CodeCompletion = model.CodeCompletion;

            model.ProjectCreatedOrOpened += Model_ProjectCreatedOrOpened;
            model.FileOpenedOrCreated += Model_FileOpenedOrCreated;
            model.ProjectCompilied += Model_ProjectCompilied;
        }

        // Compiling can take a second or two, so it runs off the UI thread.
        // The view marshals ShowCompileResults back onto its own thread.
        private async void View_Build(object sender, EventArgs e)
        {
            view.ShowBuildStarted();
            await Task.Run(() => model.Build());
        }

        private async void View_Run(object sender, EventArgs e)
        {
            view.ShowBuildStarted();
            await Task.Run(() => model.Run());
        }

        private void Model_ProjectCompilied(object sender, CompileArgs e)
        {
            view.ShowCompileResults(e);
        }

        private void View_SaveFile(object sender, FileArgs e)
        {
            model.SaveFile(e);
        }

        private void View_CreateFile(object sender, FileArgs e)
        {
            model.CreateFile(e);
        }

        private void Model_FileOpenedOrCreated(object sender, FileArgs e)
        {
            view.InitializeFile(e);
        }

        private void View_OpenFile(object sender, FileArgs e)
        {
            model.OpenFile(e);
        }

        private void View_OpenProject(object sender, ProjectArgs e)
        {
            model.OpenProject(e);
        }

        private void Model_ProjectCreatedOrOpened(object sender, InitializeProjectTreeArgs e)
        {
            view.InitializeProjectTree(e);
        }

        private void View_CreateEvent(object sender, ProjectArgs e)
        {
            model.CreateProject(e);
        }
    }
}
