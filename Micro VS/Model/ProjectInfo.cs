using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Micro_VS.Model
{
    [Serializable]
    public class ProjectInfo
    {
        public ProjectInfo()
        { }

        public ProjectInfo(string projectName, string projectFolderPath, string microSlnPath)
        {
            ProjectName = projectName ?? throw new ArgumentNullException(nameof(projectName));
            ProjectFolderPath = projectFolderPath ?? throw new ArgumentNullException(nameof(projectFolderPath));
            MicroSlnPath = microSlnPath ?? throw new ArgumentNullException(nameof(microSlnPath));
        }

        public string ProjectName { get; set; }
        public string ProjectFolderPath { get; set; }
        public string MicroSlnPath { get; set; }
        public string OutputFilePath { get; set; }
        public List<string> Files { get; set; }
    }
}
