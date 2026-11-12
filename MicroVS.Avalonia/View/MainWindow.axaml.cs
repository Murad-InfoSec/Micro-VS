using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using Micro_VS.ArgumentArgs_es;
using Micro_VS.Model.ProjectInfo_micro_sln_;
using MFile = Micro_VS.Model.ProjectInfo_micro_sln_.File;

namespace Micro_VS.View
{
    public partial class MainWindow : Window, IView
    {
        // Cmd on macOS, Ctrl elsewhere.
        private static readonly KeyModifiers CommandKey = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

        private readonly Bitmap solutionIcon = LoadIcon("IconSolution.png");
        private readonly Bitmap folderIcon = LoadIcon("Folder_16x.png");
        private readonly Bitmap csIcon = LoadIcon("CS_16x.png");

        private string currentProjectFolder;
        private string currentProjectName;

        public MainWindow()
        {
            InitializeComponent();

            SetShortcut(SaveMenuItem, new KeyGesture(Key.S, CommandKey));
            SetShortcut(SaveAllMenuItem, new KeyGesture(Key.S, CommandKey | KeyModifiers.Shift));
            SetShortcut(CutMenuItem, new KeyGesture(Key.X, CommandKey), hotKey: false);   // the editor handles these itself
            SetShortcut(CopyMenuItem, new KeyGesture(Key.C, CommandKey), hotKey: false);
            SetShortcut(PasteMenuItem, new KeyGesture(Key.V, CommandKey), hotKey: false);
            SetShortcut(BuildMenuItem, new KeyGesture(Key.B, CommandKey | KeyModifiers.Shift));
            SetShortcut(RunMenuItem, new KeyGesture(Key.F5));

            EditorTabs.SelectionChanged += (s, e) => UpdateEmptyHint();
            UpdateEmptyHint();
        }

        #region Events
        public event EventHandler<ProjectArgs> CreateProject;
        public event EventHandler<ProjectArgs> OpenProject;
        public event EventHandler<FileArgs> OpenFile;
        public event EventHandler<FileArgs> CreateFile;
        public event EventHandler<FileArgs> SaveFile;
        public event EventHandler Build;
        public event EventHandler Run;
        #endregion

        #region IView Interface Methods
        public void InitializeFile(FileArgs args)
        {
            string name = args._File.Name;
            TabItem existing = FindEditorTab(name);
            if (existing != null)
            {
                EditorTabs.SelectedItem = existing;
                return;
            }

            var editor = new TextEditor
            {
                ShowLineNumbers = true,
                LineNumbersForeground = Brushes.Blue,
                FontFamily = new FontFamily("Menlo, Consolas, Courier New, monospace"),
                FontSize = 13,
                SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#"),
                Text = args._File.Text ?? ""
            };
            editor.Options.ConvertTabsToSpaces = true;
            editor.Options.HighlightCurrentLine = true;

            var title = new TextBlock { Text = name };
            var tab = new TabItem
            {
                Header = title,
                Tag = new EditorTabInfo(name, editor, title),
                Content = editor
            };
            editor.TextChanged += (s, e) => SetDirty(tab, true);

            EditorTabs.Items.Add(tab);
            EditorTabs.SelectedItem = tab;
            UpdateEmptyHint();
            Dispatcher.UIThread.Post(() => editor.TextArea.Focus());
        }

        public void InitializeProjectTree(InitializeProjectTreeArgs args)
        {
            // A different project was created/opened: close the old project's files and results.
            if (!string.Equals(currentProjectFolder, args.ProjectFolder.Path, StringComparison.Ordinal))
            {
                EditorTabs.Items.Clear();
                ShowMessages(Array.Empty<CompilerMessage>(), Array.Empty<CompilerMessage>());
                OutputBox.Text = "";
            }
            currentProjectFolder = args.ProjectFolder.Path;
            currentProjectName = args.Name;
            Title = args.Name + " - Micro VS";

            ProjectTree.Items.Clear();
            var root = new TreeViewItem { Header = MakeHeader(solutionIcon, $"Project '{args.Name}'"), IsExpanded = true };
            root.Items.Add(BuildTree(args.ProjectFolder));
            ProjectTree.Items.Add(root);
            UpdateEmptyHint();
        }

        public void ShowBuildStarted()
        {
            OnUiThread(() =>
            {
                OutputBox.Text = $"Building {currentProjectName}...";
                ShowBottomPane();
                BottomTabs.SelectedItem = OutputTab;
            });
        }

        public void ShowCompileResults(CompileArgs args)
        {
            OnUiThread(() =>
            {
                ShowMessages(args.Errors, args.Warnings);

                var summary = new StringBuilder();
                summary.AppendLine(args.Succeeded ? "Build succeeded." : "Build failed.");
                summary.AppendLine($"{args.Errors.Count} error(s), {args.Warnings.Count} warning(s)");
                summary.AppendLine();
                foreach (string line in args.Output)
                    summary.AppendLine(line);
                OutputBox.Text = summary.ToString();

                ShowBottomPane();
                BottomTabs.SelectedItem = !args.Succeeded ? ErrorsTab
                                        : args.Warnings.Count > 0 ? WarningsTab
                                        : OutputTab;
            });
        }
        #endregion

        #region ControlsEventHandlers
        private async void CreateProject_Click(object sender, RoutedEventArgs e)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Choose location to create your project.",
                SuggestedFileName = "MyProject",
                ShowOverwritePrompt = false
            });
            string projectFolderPath = file?.TryGetLocalPath();
            if (projectFolderPath == null)
                return;

            RunSafely(() =>
            {
                string projectName = Path.GetFileName(projectFolderPath);
                CreateProject?.Invoke(this, new ProjectArgs(projectFolderPath, projectName));
                OpenFile?.Invoke(this, new FileArgs(new MFile("Program.cs")));
            });
        }

        private async void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select project",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Solution files (*.micro_sln)") { Patterns = new[] { "*.micro_sln" } } }
            });
            string path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path == null)
                return;

            RunSafely(() =>
            {
                OpenProject?.Invoke(this, new ProjectArgs(path));
                OpenFile?.Invoke(this, new FileArgs(new MFile("Program.cs")));
            });
        }

        private async void NewFile_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireProject())
                return;

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "New file",
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(currentProjectFolder),
                SuggestedFileName = "Class1.cs",
                DefaultExtension = "cs",
                FileTypeChoices = new[] { new FilePickerFileType("Cs file (*.cs)") { Patterns = new[] { "*.cs" } } }
            });
            AddFileToProject(file?.TryGetLocalPath());
        }

        // The original menu item had no handler; here it adds an existing .cs file to the project and opens it.
        private async void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireProject())
                return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open file",
                AllowMultiple = false,
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(currentProjectFolder),
                FileTypeFilter = new[] { new FilePickerFileType("Cs file (*.cs)") { Patterns = new[] { "*.cs" } } }
            });
            AddFileToProject(files.Count > 0 ? files[0].TryGetLocalPath() : null);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (EditorTabs.SelectedItem is TabItem tab)
                SaveTab(tab);
        }

        private void SaveAll_Click(object sender, RoutedEventArgs e) => SaveAllTabs();

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void Cut_Click(object sender, RoutedEventArgs e) => CurrentEditor()?.Cut();
        private void Copy_Click(object sender, RoutedEventArgs e) => CurrentEditor()?.Copy();
        private void Paste_Click(object sender, RoutedEventArgs e) => CurrentEditor()?.Paste();

        private void ProjectTreeCollapsed_Click(object sender, RoutedEventArgs e)
        {
            bool collapsed = ProjectTreeCollapsedMenuItem.IsChecked;
            TopGrid.ColumnDefinitions[2].Width = collapsed ? new GridLength(0) : new GridLength(250);
            TreePane.IsVisible = TreeSplitter.IsVisible = !collapsed;
        }

        private void ErrorsBlockCollapsed_Click(object sender, RoutedEventArgs e) =>
            SetBottomPaneCollapsed(ErrorsBlockCollapsedMenuItem.IsChecked);

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireProject())
                return;
            SaveAllTabs();
            Build?.Invoke(this, EventArgs.Empty);
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireProject())
                return;
            SaveAllTabs();
            Run?.Invoke(this, EventArgs.Empty);
        }

        // Double-clicking an error or warning jumps to it in the editor.
        private void MessagesGrid_DoubleTapped(object sender, TappedEventArgs e)
        {
            if ((sender as DataGrid)?.SelectedItem is not CompilerMessage message || message.File.Length == 0)
                return;

            OpenFile?.Invoke(this, new FileArgs(new MFile(message.File)));
            TextEditor editor = (FindEditorTab(message.File)?.Tag as EditorTabInfo)?.Editor;
            if (editor == null)
                return;

            int line = Math.Clamp(message.Line, 1, Math.Max(1, editor.Document.LineCount));
            editor.TextArea.Caret.Line = line;
            editor.TextArea.Caret.Column = Math.Max(1, message.Column);
            editor.ScrollTo(line, Math.Max(1, message.Column));
            Dispatcher.UIThread.Post(() => editor.TextArea.Focus());
        }
        #endregion

        #region MyPrivateMethods
        private TreeViewItem BuildTree(Folder folder)
        {
            var node = new TreeViewItem { Header = MakeHeader(folderIcon, folder.Name), Tag = folder.Path, IsExpanded = true };

            foreach (var subdir in folder.Components.OfType<Folder>())
                node.Items.Add(BuildTree(subdir));

            foreach (var file in folder.Components.OfType<MFile>())
            {
                var fileNode = new TreeViewItem { Header = MakeHeader(csIcon, file.Name), Tag = file.Path };
                string name = file.Name;
                fileNode.DoubleTapped += (s, e) =>
                {
                    e.Handled = true;
                    OpenFile?.Invoke(this, new FileArgs(new MFile(name)));
                };
                node.Items.Add(fileNode);
            }
            return node;
        }

        private static Control MakeHeader(Bitmap icon, string text) =>
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new Image { Source = icon, Width = 16, Height = 16 },
                    new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }
                }
            };

        private void AddFileToProject(string path)
        {
            if (path == null)
                return;
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                path += ".cs";

            RunSafely(() =>
            {
                string name = Path.GetFileName(path);
                CreateFile?.Invoke(this, new FileArgs(new MFile(name, path)));
                OpenFile?.Invoke(this, new FileArgs(new MFile(name)));
            });
        }

        private void SaveTab(TabItem tab)
        {
            if (tab.Tag is not EditorTabInfo info)
                return;
            SaveFile?.Invoke(this, new FileArgs(new MFile(info.FileName) { Text = info.Editor.Text }));
            SetDirty(tab, false);
        }

        private void SaveAllTabs()
        {
            foreach (TabItem tab in EditorTabs.Items.OfType<TabItem>())
                SaveTab(tab);
        }

        private static void SetDirty(TabItem tab, bool dirty)
        {
            if (tab.Tag is EditorTabInfo info)
                info.Title.Text = dirty ? info.FileName + " *" : info.FileName;
        }

        private TabItem FindEditorTab(string fileName) =>
            EditorTabs.Items.OfType<TabItem>().FirstOrDefault(t => (t.Tag as EditorTabInfo)?.FileName == fileName);

        private TextEditor CurrentEditor() => ((EditorTabs.SelectedItem as TabItem)?.Tag as EditorTabInfo)?.Editor;

        private void ShowMessages(IReadOnlyList<CompilerMessage> errors, IReadOnlyList<CompilerMessage> warnings)
        {
            ErrorsGrid.ItemsSource = errors;
            WarningsGrid.ItemsSource = warnings;
            ErrorsTab.Header = errors.Count > 0 ? $"Errors ({errors.Count})" : "Errors";
            WarningsTab.Header = warnings.Count > 0 ? $"Warnings ({warnings.Count})" : "Warnings";
        }

        private void ShowBottomPane()
        {
            ErrorsBlockCollapsedMenuItem.IsChecked = false;
            SetBottomPaneCollapsed(false);
        }

        private void SetBottomPaneCollapsed(bool collapsed)
        {
            RootGrid.RowDefinitions[2].Height = collapsed ? new GridLength(0) : new GridLength(220);
            BottomTabs.IsVisible = BottomSplitter.IsVisible = !collapsed;
        }

        private void UpdateEmptyHint()
        {
            EmptyHint.IsVisible = EditorTabs.Items.Count == 0;
            if (currentProjectFolder != null)
                EmptyHint.Text = "Double-click a file in Solution Explorer to open it.";
        }

        private bool RequireProject()
        {
            if (currentProjectFolder != null)
                return true;
            OutputBox.Text = "Create or open a project first (File > Create Project / Open Project).";
            ShowBottomPane();
            BottomTabs.SelectedItem = OutputTab;
            return false;
        }

        private void RunSafely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                OutputBox.Text = "Error: " + ex.Message;
                ShowBottomPane();
                BottomTabs.SelectedItem = OutputTab;
            }
        }

        private static void OnUiThread(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action);
        }

        private static void SetShortcut(MenuItem item, KeyGesture gesture, bool hotKey = true)
        {
            item.InputGesture = gesture;
            if (hotKey)
                item.HotKey = gesture;
        }

        private static Bitmap LoadIcon(string fileName) =>
            new Bitmap(AssetLoader.Open(new Uri("avares://MicroVS/Assets/" + fileName)));
        #endregion

        private sealed record EditorTabInfo(string FileName, TextEditor Editor, TextBlock Title);
    }
}
