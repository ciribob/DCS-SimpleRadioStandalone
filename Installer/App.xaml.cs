using System.Globalization;
using System.Threading;
using System.Windows;

namespace Installer
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        //public App()
        //{
        //    System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("zh-CN");
        //}
        public string[] Arguments = new string[0];
        private void ApplicationStartup(object sender, StartupEventArgs e)
        {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-CN");
            if (e.Args.Length > 0)
            {
                Arguments = e.Args;
            }
           
        }
    }
}