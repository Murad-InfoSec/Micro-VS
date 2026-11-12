using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Micro_VS.ArgumentArgs_es
{
    public class ProjectArgs : EventArgs
    {
        public ProjectArgs(string path)
        {
            Path = path  ?? throw new ArgumentNullException(nameof(path));
        }

        public ProjectArgs(string path, string name) : this(path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Path { get; set; }
        public string Name { get; set; }
    }
}
