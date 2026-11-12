using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Micro_VS.Model.ProjectInfo_micro_sln_
{
   [Serializable]

    public class File : Component
    {
        public File() { }
        public File(string name) { this.Name = name; }
        public File(string name, string path) : base(name, path)   {}
        public File(string name, string path , string text) : base(name, path) { this.Text = text; }
        public string Text { get; set; }
    }
}
