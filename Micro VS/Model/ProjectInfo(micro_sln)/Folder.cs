using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Micro_VS.Model.ProjectInfo_micro_sln_
{
    [Serializable]
    public class Folder : Component
    {
        public Folder() { Components = new List<Component>(); }
        public Folder(string name, string path) : base(name, path)
        { Components = new List<Component>(); }
        public List<Component> Components { get; set; }
    }                                                                  
}
