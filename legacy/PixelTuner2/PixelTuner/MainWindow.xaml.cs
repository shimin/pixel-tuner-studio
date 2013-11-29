using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PixelTuner.Properties;
using Workspace.Layout;
using Workspace.Layout.Serialization;

using Core;
using Communication.Service;
using PixelTuner.ViewModel;
using PixelTunerAutoupdater;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string _layoutString = Settings.Default.LayoutString;
        System.ComponentModel.BackgroundWorker _updatesCheckWorker = new System.ComponentModel.BackgroundWorker();

        public MainWindow()
        {
            Settings.Default.Reload();
            string culture = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            DeviceHelper.StartSeeking();
            ResourceDictionary d = null;
            
            foreach (ResourceDictionary dictionary in Application.Current.Resources.MergedDictionaries)
            {                
                d = dictionary;
                if (d.Source.OriginalString.Contains(culture)) break;
            }
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(d);
            InitializeComponent();
            this.DataContext = RootViewModel.This = (RootViewModel)Application.Current.TryFindResource("RootContext");
        }

        private void Window_Loaded(object sender, RoutedEventArgs ev)
        {
            _updatesCheckWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(_updatesCheckWorker_DoWork);
            _updatesCheckWorker.RunWorkerAsync();
        }

        private static void UiInvoke(Action a)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(a);
        }

        void _updatesCheckWorker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                AutoUpdater autoUpdater = new AutoUpdater(this);
                if (autoUpdater.UpdateNeeded())
                {
                    UiInvoke(() =>
                    {
                        RootViewModel.This.UpdatesAvailable = true;
                    });
                }
                else
                {
                    UiInvoke(() =>
                    {
                        RootViewModel.This.UpdatesAvailable = false;
                    });
                }
            }
            catch
            {

            }
        }

        private void ExitApplication(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DeviceHelper.StopSeeking();
            Settings.Default.Save();
            var filesToClose = new List<FileViewModel>(RootViewModel.This.Files);
            foreach (var f in filesToClose)
            {
                if (!RootViewModel.This.Close(f)) e.Cancel = true;
            }
        }

        private void MainWnd_Activated(object sender, EventArgs e)
        {
            this.Focus();   // без явной установки фокуса не работают горячие клавиши
        }

    }
}
