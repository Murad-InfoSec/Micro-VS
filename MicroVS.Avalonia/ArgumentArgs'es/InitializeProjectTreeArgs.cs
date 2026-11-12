using Micro_VS.Model.ProjectInfo_micro_sln_;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Micro_VS.ArgumentArgs_es
{
    public class InitializeProjectTreeArgs : EventArgs
    {
        public InitializeProjectTreeArgs(string name, Folder projectFolder)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ProjectFolder = projectFolder ?? throw new ArgumentNullException(nameof(projectFolder));
        }

        public string Name { get; set; }
        public Folder ProjectFolder { get; set; }
    }
}
