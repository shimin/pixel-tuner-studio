using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Diagnostics;
using Workspace.Layout.Serialization;
using System.IO;
using System.Collections.ObjectModel;
using Core;
using PixelTuner.Service;
using PixelTunerAutoupdater;
using System.Net;

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
                        (this.DataContext as RootViewModel).UpdatesAvailable = System.Windows.Visibility.Visible;
                    });
                    
                }
                else
                {
                    UiInvoke(() =>
                    {
                        (this.DataContext as RootViewModel).UpdatesAvailable = System.Windows.Visibility.Collapsed;
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

        private void NewDocument(object sender, RoutedEventArgs e)
        {

        }

        private void ShowDeviceList(object sender, RoutedEventArgs e)
        {
        //    _deviceList.DockManager = this.dockManager;
        //    _deviceList.Show();
        }

        private void ShowProjectList(object sender, RoutedEventArgs e)
        {
            //_projectList.DockManager = this.dockManager;
            //_projectList.Show();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DeviceHelper.StopSeeking();
            Settings.Default.Save();
        }

        private void LayoutRoot_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var activeContent = ((LayoutRoot)sender).ActiveContent;
            if (e.PropertyName == "ActiveContent" && activeContent != null)
            {
                Debug.WriteLine(string.Format("ActiveContent-> {0}", activeContent));
            }
        }

        private void MainWnd_Activated(object sender, EventArgs e)
        {
            this.Focus();   // без явной установки фокуса не работают горячие клавиши
        }

        private void boxThumbnails_DragOver(object sender, DragEventArgs e)
        {
            if (this.DataContext as RootViewModel != null)
            {
                (this.DataContext as RootViewModel).boxThumbnails_DragOver(sender, e);
            }
        }

        private void boxThumbnails_DragEnter(object sender, DragEventArgs e)
        {
            if (this.DataContext as RootViewModel != null)
            {
                (this.DataContext as RootViewModel).boxThumbnails_DragEnter(sender, e);
            }
        }

        private void boxThumbnails_Drop(object sender, DragEventArgs e)
        {
            if (this.DataContext as RootViewModel != null)
            {
                (this.DataContext as RootViewModel).boxThumbnails_Drop(sender, e);
            }
        }

        private void boxThumbnails_KeyDown(object sender, KeyEventArgs e)
        {
            if (this.DataContext as RootViewModel != null)
            {
                (this.DataContext as RootViewModel).boxThumbnails_KeyDown(sender, e);
            }
        }
    }
}
