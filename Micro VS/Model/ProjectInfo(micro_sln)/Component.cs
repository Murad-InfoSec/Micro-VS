using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Micro_VS.Model.ProjectInfo_micro_sln_
{
    /// <summary>
    /// Implementation of Composite pattern.
    /// </summary>
    public abstract class Component
    {
        public Component()
        {
        }

        public Component(string name, string path)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public string Name { get; set; }
        public string Path { get; set; }
    }
}
