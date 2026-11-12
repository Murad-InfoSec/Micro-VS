using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Micro_VS.ArgumentArgs_es;
using Micro_VS.Model.ProjectInfo_micro_sln_;

namespace Micro_VS.View
{
    public partial class MainForm : Form, IView
    {
        public MainForm()
        {
            InitializeComponent();
            treeView1.ImageList = new ImageList();
            treeView1.ImageList.Images.Add(Properties.Resources.IconSolution);
            treeView1.ImageList.Images.Add(Properties.Resources.Folder_16x);
            treeView1.ImageList.Images.Add(Properties.Resources.CS_16x);
        }

        #region Events
        public event EventHandler<ProjectArgs> CreateProject;
        public event EventHandler<ProjectArgs> OpenProject;
        public event EventHandler<FileArgs> OpenFile;
        public event EventHandler<FileArgs> CreateFile;
        public event EventHandler<FileArgs> SaveFile;
        public event EventHandler Run;

        #endregion

        #region IView Interface Methods
        public void InitializeFile(FileArgs args )
        {
            FastColoredTextBoxNS.FastColoredTextBox fctb = new FastColoredTextBoxNS.FastColoredTextBox();
            fctb.Dock = DockStyle.Fill;
            fctb.LineNumberColor = Color.Blue;
            fctb.Language = FastColoredTextBoxNS.Language.CSharp;
            fctb.Text = args._File.Text;
            tabControl2.TabPages.Add(args._File.Name, args._File.Name);
            tabControl2.TabPages[args._File.Name].Controls.Add(fctb);
        }

        public void InitializeProjectTree(InitializeProjectTreeArgs args)
        {
            treeView1.Nodes.Clear();
            this.Text = args.Name;
            treeView1.Nodes.Add("Project", $"Project '{args.Name}'", 0, 0);
            BuildTree(args.ProjectFolder, treeView1.Nodes[0].Nodes);
        }

        public void ShowCompileResults(CompileArgs args)
        {
            ErrorsDataGridView.DataSource = ToGridRows(args.Errors);
            WarningsDataGridView.DataSource = ToGridRows(args.Warnings);

            var summary = new StringBuilder();
            summary.AppendLine(args.Succeeded
                ? $"Build succeeded: {args.OutputAssembly}"
                : "Build failed.");
            summary.AppendLine($"{args.Errors.Count} error(s), {args.Warnings.Count} warning(s)");
            summary.AppendLine();
            foreach (string line in args.Output)
                summary.AppendLine(line);
            OutputTextBox.Text = summary.ToString();

            HorizontalSplitContainer.Panel2Collapsed = false;
            errorsBlockCollapsedToolStripMenuItem.Checked = false;
            tabControl1.SelectedTab = !args.Succeeded ? tabPage1
                                    : args.Warnings.Count > 0 ? tabPage2
                                    : tabPage3;
        }
        #endregion

        #region ControlsEventHandlers
        private void projectTreeCollapsedToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            if (projectTreeCollapsedToolStripMenuItem.Checked)
            {
                VerticalSplitContainer.Panel2Collapsed = true;
            }
            else
            {
                VerticalSplitContainer.Panel2Collapsed = false;
            }
        }

        private void errorsBlockCollapsedToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            if (errorsBlockCollapsedToolStripMenuItem.Checked)
            {
                HorizontalSplitContainer.Panel2Collapsed = true;
            }
            else
            {
                HorizontalSplitContainer.Panel2Collapsed = false;
            }
        }

        private void buil_tool_item_Click(object sender, EventArgs e)
        {

        }

        private void createProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog sd = new SaveFileDialog();
            sd.Title = "Choose location to create your project.";
            if (sd.ShowDialog() == DialogResult.OK)
            {
                string ProjectName = Path.GetFileName(sd.FileName);
                string ProjectFolderPath = sd.FileName;
                CreateProject.Invoke(this, new ProjectArgs(ProjectFolderPath, ProjectName));
                OpenFile.Invoke(this,new FileArgs(new Model.ProjectInfo_micro_sln_.File("Program.cs")));
            }
        }

        private void openProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog of = new OpenFileDialog();
            of.Title = "Select project";
            of.Filter = "Solution files (*.micro_sln)|*.micro_sln";
            if (of.ShowDialog() == DialogResult.OK)
            {
                OpenProject.Invoke(this, new ProjectArgs(of.FileName));
                OpenFile.Invoke(this, new FileArgs(new Model.ProjectInfo_micro_sln_.File("Program.cs")));
            }
        }


        private void newFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog sf = new SaveFileDialog();
            string initDir = treeView1.Nodes[0].Nodes[0].Tag as string;
            sf.InitialDirectory = initDir;
            sf.Filter = "Cs file (*.cs)|*.cs";
            sf.RestoreDirectory = true;
            if(sf.ShowDialog() == DialogResult.OK)
            {
                string name = Path.GetFileName(sf.FileName);
                CreateFile.Invoke(this,new FileArgs(new Model.ProjectInfo_micro_sln_.File(name,sf.FileName)));
            }
        }

        private void treeView1_DoubleClick(object sender, EventArgs e)
        {
            string selectedNodePath = treeView1.SelectedNode.Tag as string;
            FileAttributes attr = System.IO.File.GetAttributes(selectedNodePath);
            if (!(attr == FileAttributes.Directory))
            {
                OpenFile.Invoke(this, new FileArgs(new Model.ProjectInfo_micro_sln_.File(treeView1.SelectedNode.Name)));
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Model.ProjectInfo_micro_sln_.File toSave = new Model.ProjectInfo_micro_sln_.File(tabControl2.SelectedTab.Text);
            toSave.Text = tabControl2.SelectedTab.Controls[0].Text;
            SaveFile.Invoke(this, new FileArgs(toSave));                
        }
       
        private void saveAllFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach(TabPage item in tabControl2.TabPages)
            {
                Model.ProjectInfo_micro_sln_.File toSave = new Model.ProjectInfo_micro_sln_.File(item.Text);
                toSave.Text = item.Controls[0].Text;
                SaveFile.Invoke(this, new FileArgs(toSave));
            }
        }
        
        #endregion

        #region MyPrivateMethods
        private static object ToGridRows(IEnumerable<System.CodeDom.Compiler.CompilerError> items)
        {
            return items.Select(e => new
            {
                Code = e.ErrorNumber,
                Description = e.ErrorText,
                File = string.IsNullOrEmpty(e.FileName) ? "" : Path.GetFileName(e.FileName),
                Line = e.Line,
                Column = e.Column
            }).ToList();
        }

        private void BuildTree(Folder folder, TreeNodeCollection addInMe)
        {
            TreeNode curNode = addInMe.Add(folder.Name, folder.Name, 1, 1);
            curNode.Tag = folder.Path;

            foreach (var subdir in folder.Components.Where(a => a is Folder))
            {
                BuildTree(subdir as Folder, curNode.Nodes);
            }

            foreach (var file in folder.Components.Where(a => a is Model.ProjectInfo_micro_sln_.File))
            {
                curNode.Nodes.Add(file.Name, file.Name, 2, 2).Tag = file.Path;
            }
        }
        #endregion

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(tabControl2.TabPages.Count != 0)
            {
                (tabControl2.SelectedTab.Controls[0] as FastColoredTextBoxNS.FastColoredTextBox).Cut();
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tabControl2.TabPages.Count != 0)
            {
                (tabControl2.SelectedTab.Controls[0] as FastColoredTextBoxNS.FastColoredTextBox).Copy();
            }
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tabControl2.TabPages.Count != 0)
            {
                (tabControl2.SelectedTab.Controls[0] as FastColoredTextBoxNS.FastColoredTextBox).Paste();
            }
        }

        private void run_tool_item_Click(object sender, EventArgs e)
        {
            saveAllFilesToolStripMenuItem_Click(this,new EventArgs());
            Run.Invoke(this,new EventArgs());
        }
    }
}
