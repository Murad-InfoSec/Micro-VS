using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Micro_VS.Model.ProjectInfo_micro_sln_
{
    /// <summary>
    /// Implements Prototype pattern .
    /// </summary>
   [Serializable]
    public class Project : ICloneable 
    {
        public Project()
        {
            
        }

        public Project(string name, File microSln , File ProgramCs , string projectFolderPath)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MicroSln = microSln ?? throw new ArgumentNullException(nameof(microSln));
            ProjectFolder = new Folder(name, projectFolderPath);
            ProjectFolder.Components.Add(ProgramCs);
        }

        public string Name { get; set; }
        public File MicroSln { get; set; }
        public File OutputFile { get; set; }
        public Folder ProjectFolder { get; set; }

        public object Clone()
        {
            throw new NotImplementedException();
        }
    }
}
