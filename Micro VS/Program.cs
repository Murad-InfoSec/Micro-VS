using Micro_VS.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Micro_VS.Presenter;

namespace Micro_VS
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var view = new MainForm();


            var p = new Presenter.Presenter(view,new Model.Model());

            Application.Run(view);
        }
    }
}
