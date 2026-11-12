using Micro_VS.Model.ProjectInfo_micro_sln_;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Micro_VS.ArgumentArgs_es
{
    public class FileArgs : EventArgs
    {
        public FileArgs(File File)
        {
            _File = File ?? throw new ArgumentNullException(nameof(File));
        }

        public File _File { get; set; }
    }
}
