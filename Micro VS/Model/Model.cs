using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Micro_VS.ArgumentArgs_es;
using Micro_VS.Model.ProjectInfo_micro_sln_;
using System.Xml.Serialization;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Diagnostics;

namespace Micro_VS.Model
{
    public class Model : IModel
    {
        public Project CurrentProject { get; set; }

        public event EventHandler<InitializeProjectTreeArgs> ProjectCreatedOrOpened;
        public event EventHandler<FileArgs> FileOpenedOrCreated;
        public event EventHandler<CompileArgs> ProjectCompilied;

        public void CreateProject(ProjectArgs args)
        {
            if (CurrentProject == null)
            {
                //Creates project folder .
                string ProjectDirPath = Directory.CreateDirectory(args.Path).FullName;

                //Determines micro_sln and program.cs files paths.
                string MicroSlnPath = Path.Combine(ProjectDirPath, args.Name + ".micro_sln");
                string ProgramCsPath = Path.Combine(ProjectDirPath, "Program.cs");

                //Creates micro_sln file and program.cs file . 
                ProjectInfo_micro_sln_.File microSln = new ProjectInfo_micro_sln_.File(args.Name + @".micro_sln", MicroSlnPath);
                ProjectInfo_micro_sln_.File programCs = new ProjectInfo_micro_sln_.File("Program.cs", ProgramCsPath);
                FileStream ProgramCsStream = new FileStream(ProgramCsPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                ProgramCsStream.Dispose();

                //Creates project .
                CurrentProject = new Project(args.Name, microSln, programCs, ProjectDirPath);

                //Serializes Project in micro_sln file .
                SaveProjectSln();
                //Notice the Presenter the project is Created . 

                ProjectCreatedOrOpened.Invoke(this, new InitializeProjectTreeArgs(CurrentProject.Name, CurrentProject.ProjectFolder));
            }
        }

        public void OpenProject(ProjectArgs args)
        {
            if (CurrentProject == null)
            {
                using (FileStream fs = new FileStream(args.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    XmlSerializer xmlDeserializer = new XmlSerializer(typeof(Project));
                    CurrentProject = (Project)xmlDeserializer.Deserialize(fs);
                }
                string ProgramCsText = System.IO.File.ReadAllText(CurrentProject.ProjectFolder.Components.Find(a => a.Name == "Program.cs").Path);

                ProjectCreatedOrOpened.Invoke(this, new InitializeProjectTreeArgs(CurrentProject.Name, CurrentProject.ProjectFolder));

            }
        }

        private void SaveProjectSln()
        {
            //Serializes Project in micro_sln file .
            using (FileStream micro_sln_file = new FileStream(CurrentProject.MicroSln.Path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Project));
                xmlSerializer.Serialize(micro_sln_file, CurrentProject);
            }
        }

        public void OpenFile(FileArgs args)
        {
            args._File = this.CurrentProject.ProjectFolder.Components.Find(a => a.Name == args._File.Name) as ProjectInfo_micro_sln_.File;
            args._File.Text = System.IO.File.ReadAllText(args._File.Path);
            FileOpenedOrCreated.Invoke(this, args);
        }

        public void CreateFile(FileArgs args)
        {
            FileStream newFile = System.IO.File.Create(args._File.Path);
            newFile.Dispose();
            CurrentProject.ProjectFolder.Components.Add(args._File);

            SaveProjectSln();
            ProjectCreatedOrOpened(this, new InitializeProjectTreeArgs(CurrentProject.Name, CurrentProject.ProjectFolder));
        }

        public void SaveFile(FileArgs args)
        {
            var fileToSave =  CurrentProject.ProjectFolder.Components.Find(a => a.Name == args._File.Name) as ProjectInfo_micro_sln_.File;
            SaveFile(fileToSave.Path, args._File.Text);
        }

        private void SaveFile(string path, string text)
        {
            using (FileStream fs = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.Read))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.Write(text);
                }
            }
        }

        // Assemblies every compiled program can use. Windows csc.exe adds most of these
        // implicitly via csc.rsp, but other compilers (e.g. Mono) do not, so list them explicitly.
        private static readonly string[] DefaultReferences =
        {
            "System.dll", "System.Core.dll", "System.Data.dll", "System.Xml.dll",
            "System.Xml.Linq.dll", "Microsoft.CSharp.dll", "System.Drawing.dll", "System.Windows.Forms.dll"
        };

        public void Run()
        {
            if (CurrentProject == null)
                return;

            string outputAssembly = Path.Combine(CurrentProject.ProjectFolder.Path, CurrentProject.Name + ".exe");

            CompilerParameters parameters = new CompilerParameters(DefaultReferences, outputAssembly, true);
            parameters.GenerateExecutable = true;
            parameters.GenerateInMemory = false;
            parameters.WarningLevel = 3;
            parameters.CompilerOptions = "/optimize";

            string[] fileNames = CurrentProject.ProjectFolder.Components
                .Where(a => a is ProjectInfo_micro_sln_.File && a.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Path)
                .ToArray();

            List<string> output = new List<string>();
            List<CompilerError> errors = new List<CompilerError>();
            List<CompilerError> warnings = new List<CompilerError>();

            if (fileNames.Length == 0)
            {
                errors.Add(new CompilerError(null, 0, 0, "", "The project has no .cs files to compile."));
            }
            else
            {
                using (CSharpCodeProvider provider = new CSharpCodeProvider())
                {
                    CompilerResults res = provider.CompileAssemblyFromFile(parameters, fileNames);
                    output.AddRange(res.Output.Cast<string>());
                    foreach (CompilerError err in res.Errors)
                        (err.IsWarning ? warnings : errors).Add(err);
                }
            }

            if (errors.Count == 0)
            {
                try
                {
                    StartProgram(outputAssembly);
                    output.Add("Started " + outputAssembly);
                }
                catch (Exception ex)
                {
                    output.Add("Build succeeded, but the program could not be started: " + ex.Message);
                }
            }

            ProjectCompilied?.Invoke(this, new CompileArgs(errors, warnings, output, outputAssembly));
        }

        private static void StartProgram(string exePath)
        {
            ProcessStartInfo psi;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Opens the program in its own console window.
                psi = new ProcessStartInfo(exePath) { UseShellExecute = true };
            }
            else
            {
                // On macOS/Linux a .NET exe has to be run through Mono; output goes to the IDE's terminal.
                psi = new ProcessStartInfo("mono", "\"" + exePath + "\"") { UseShellExecute = false };
            }
            psi.WorkingDirectory = Path.GetDirectoryName(exePath);
            Process.Start(psi);
        }
    }
}

