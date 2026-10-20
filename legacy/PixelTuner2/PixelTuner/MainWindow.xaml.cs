using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;

using Communication.Service;
using PixelTuner.Properties;
using PixelTuner.ViewModel;
using SharpGL.WPF;
using SharpGL;

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
        static string _dataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public MainWindow()
        {
            this.DataContext = RootViewModel.This = (RootViewModel)Application.Current.TryFindResource("RootContext");
            TestOpenGL();
            Settings.Default.Reload();
            string culture = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            ResourceDictionary d = null;
            
            foreach (ResourceDictionary dictionary in Application.Current.Resources.MergedDictionaries)
            {                
                d = dictionary;
                if (d.Source.OriginalString.Contains(culture)) break;
            }
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(d);
            Loaded += (s, e) => { DeviceHelper.StartSeeking(); };
            InitializeComponent();
            RecentFileList.MenuClick += (s, e) => RootViewModel.This.Open(e.Filepath);
            RootViewModel.This.LastUsedFileHandler = (p) => RecentFileList.InsertFile(p);
        }
        
        private void MainWnd_Activated(object sender, EventArgs e)
        {
            this.Focus();   // без явной установки фокуса не работают горячие клавиши
        }

        private void Window_Loaded(object sender, RoutedEventArgs ev)
        {
            var serializer = new Workspace.Layout.Serialization.XmlLayoutSerializer(dockManager);
            serializer.LayoutSerializationCallback += (s, args) =>
            {
                args.Content = args.Content;
            };

            if (File.Exists(Path.Combine(_dataPath, @"PixelTuner 2\" + AppID + "Dock.config")))
                serializer.Deserialize((Path.Combine(_dataPath, @"PixelTuner 2\" + AppID + "Dock.config")));
        }

        private void TestOpenGL()
        {
            try
            {
                OpenGL glx = new OpenGL();
                glx.Create(SharpGL.Version.OpenGLVersion.OpenGL3_0, RenderContextType.HiddenWindow, 1, 1, 1, null);
                var version = glx.Version;
                if (version[0] == '3')
                    RootViewModel.This.IsOpenGLAvailable = true;
            }
            catch (Exception ex)
            {
                
            }
        }

        private void MainWnd_Unloaded(object sender, RoutedEventArgs e)
        {
            var serializer = new Workspace.Layout.Serialization.XmlLayoutSerializer(dockManager);
            var dirPath = Path.Combine(_dataPath, @"PixelTuner 2");
            var filePath = Path.Combine(dirPath, AppID + "Dock.config");
            try
            {
                if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
                serializer.Serialize(filePath);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static void UiInvoke(Action a)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(a);
        }
        
        private void ExitApplication(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.Save();
            var filesToClose = new List<FileViewModel>(RootViewModel.This.Files);
            foreach (var f in filesToClose)
            {
                if (!RootViewModel.This.Close(f)) e.Cancel = true;
                else f.Dispose();
            }

            // Only shut communication down if the window really closes: before, cancelling
            // the "save changes?" prompt left the app running with device search stopped.
            if (e.Cancel) return;

            if (RootViewModel.This.UdpСlient != null)
            {
                try
                {
                    RootViewModel.This.UdpСlient.Close();
                    RootViewModel.This.UdpСlient = null;
                }
                catch { }
            }
            DeviceHelper.StopSeeking();
        }

        private void boxSwitchInsertMode_Click(object sender, RoutedEventArgs e)
        {
            RootViewModel.This.CutNextItems = !RootViewModel.This.CutNextItems;
        }

        #region Обработка нажатий кнопок клавиатуры

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Handled) return;
            if (e.Key == Key.S && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                var doc = RootViewModel.This.ActiveDocument;
                if (doc != null && doc.SoundPlayer.WaveformData != null && doc.SoundPlayer.ChannelPosition > 0)
                {
                    var l = doc.SelectedTimeline;
                    if (l != null)
                    {
                        var time = l.Start.Value.AddMilliseconds((int)(doc.SoundPlayer.CurrentTime.TotalMilliseconds / 50) * 50);
                        var item = doc.SelectedTimeline.FindItemByTime(time);
                        item?.Split(time - item.StartTime.Value);
                    }
                }
            }
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

            if (e.Key == Key.Enter && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                var doc = RootViewModel.This.ActiveDocument;
                if (doc != null)
                {
                    if (doc.SoundPlayer.IsPlaying)
                    {
                        if (doc.PauseAudio.CanExecute(null))
                            doc.PauseAudio.Execute(null);
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
                        RootViewModel.This.ActiveDocument.Undo();
                        break;
                    case Key.Y:
                        RootViewModel.This.ActiveDocument.Redo();
                        break;
                }
            }
        }

        #endregion

    }
}
