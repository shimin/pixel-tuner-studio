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
        string AppID = "PixelTuner";

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
            RecentFileList.MenuClick += (s, e) => RootViewModel.This.Open(e.Filepath);
            RootViewModel.This.LastUsedFileHandler = (p) => RecentFileList.InsertFile(p);
        }

        private void Window_Loaded(object sender, RoutedEventArgs ev)
        {
            var serializer = new Workspace.Layout.Serialization.XmlLayoutSerializer(dockManager);
            serializer.LayoutSerializationCallback += (s, args) =>
            {
                args.Content = args.Content;
            };

            if (File.Exists(@".\" + AppID + "Dock.config"))
                serializer.Deserialize(@".\" + AppID + "Dock.config");
            
            _updatesCheckWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(_updatesCheckWorker_DoWork);
            _updatesCheckWorker.RunWorkerAsync();
        }
        
        private void MainWnd_Unloaded(object sender, RoutedEventArgs e)
        {
            var serializer = new Workspace.Layout.Serialization.XmlLayoutSerializer(dockManager);
            serializer.Serialize(@".\" + AppID + "Dock.config");
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

        #region Обработка нажатий кнопок клавиатуры

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key == Key.Space)
            {
                var doc = RootViewModel.This.ActiveDocument;
                if (doc != null)
                {
                    if (doc.SoundPlayer.IsPlaying)
                    {
                        if (doc.StopAudio.CanExecute(null))
                            doc.StopAudio.Execute(null);
                    }
                    else
                    {
                        if (doc.PlayAudio.CanExecute(null))
                            doc.PlayAudio.Execute(null);
                    }
                }
            }
            else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        //RootViewModel.This.ActiveDocument.Undo();
                        break;
                    case Key.Y:
                        //RootViewModel.This.ActiveDocument.Redo();
                        break;
                }
            }
        }

        #endregion

        private void boxSwitchInsertMode_Click(object sender, RoutedEventArgs e)
        {
            RootViewModel.This.CutNextItems = !RootViewModel.This.CutNextItems;
        }

    }
}
