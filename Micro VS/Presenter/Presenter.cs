using Micro_VS.ArgumentArgs_es;
using Micro_VS.Model;
using Micro_VS.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Micro_VS.Presenter
{
    public class Presenter
    {
        private IView view;
        private IModel model;

        public Presenter(IView view , IModel model)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.model = model ?? throw new ArgumentNullException(nameof(model));

            view.CreateProject += View_CreateEvent;
            view.OpenProject += View_OpenProject;
            view.OpenFile += View_OpenFile;
            view.CreateFile += View_CreateFile;
            view.SaveFile += View_SaveFile;
            view.SaveFile += View_SaveFile1;
            view.Run += View_Run;

            model.ProjectCreatedOrOpened += Model_ProjectCreatedOrOpened;
            model.FileOpenedOrCreated += Model_FileOpenedOrCreated;
            model.ProjectCompilied += Model_ProjectCompilied;
        }

        private void Model_ProjectCompilied(object sender, CompileArgs e)
        {
            view.ShowCompileResults(e);
        }

        private void View_Run(object sender, EventArgs e)
        {
            model.Run();
        }

        private void View_SaveFile1(object sender, FileArgs e)
        {
            
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
