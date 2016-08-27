using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_DispatcherUnhandledException(object sender, 
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                HandleException(e.Exception);
                e.Handled = true;
            }
            catch
            {
                MessageBox.Show(e.Exception.Message + "\n" + e.Exception.InnerException.Message, Application.Current.MainWindow.Title);
            }
        }
        
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            try
            {
                HandleException(ex);
            }
            catch
            {

            }
        }

        private void HandleException(Exception e)
        {
            System.Text.StringBuilder message = new System.Text.StringBuilder();
            Exception ex = e;
            int level = 0;
            while (ex != null)
            {
                AppendText(ex.Message, message, level);
                AppendText(ex.StackTrace, message, level);
                ex = ex.InnerException;
                level++;
            }
            try
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                System.IO.File.WriteAllText(System.IO.Path.Combine(path, "PixelTuner 2\\LastUnhandledException.txt"), message.ToString());
            }
            catch { }
        }

        private void AppendText(string text, System.Text.StringBuilder result, int level)
        {
            while (level > 0)
                result.Append("\t");

            result.AppendLine(text);
        }
    }
}
