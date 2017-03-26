using System;
using System.Windows.Forms;

namespace BoggleGUI
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var context = BoggleClientContext.GetContext();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            context.RunNew();
            try
            {
                Application.Run(context);
            }
            catch (Exception)
            {
                // no-op
            }
        }
    }
}
