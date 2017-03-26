using System.Windows.Forms;

namespace BoggleGUI
{
    class BoggleClientContext : ApplicationContext
    {
        private static BoggleClientContext context;

        private BoggleClientContext() {}

        public static BoggleClientContext GetContext()
        {
            return context ?? (context = new BoggleClientContext());
        }

        public void RunNew()
        {
            var controller = new Controller();
            controller.WindowsClosedEvent += Application.Exit;
            controller.Run();
        }
    }
}
