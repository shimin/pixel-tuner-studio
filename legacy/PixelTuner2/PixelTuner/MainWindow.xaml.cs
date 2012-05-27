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
using AvalonDock.Layout;
using System.Diagnostics;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //ListWindow _projectList = new ListWindow();
        //ListWindow _deviceList = new ListWindow();
        string _layoutString = Settings.Default.LayoutString;

        public MainWindow()
        {
            Settings.Default.Reload();
            //string culture = System.Threading.Thread.CurrentThread.CurrentCulture.NativeName;
            string culture = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            //System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(culture);
            //System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(culture);

            // Reload all the merged dictionaries to reset the resources.
            ResourceDictionary d = null;
            
            foreach (ResourceDictionary dictionary in Application.Current.Resources.MergedDictionaries)
            {                
                d = dictionary;
                if (d.Source.OriginalString.Contains(culture)) break;
            }
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(d);
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //dockManager.ParentWindow = this;

            //if (!string.IsNullOrEmpty(_layoutString))
            //    dockManager.RestoreLayoutFromXml(_layoutString, ExtractListWindow);
            //else
            {
                //_projectList.Title = (string)Application.Current.FindResource("Menu_Window_Projects");
                //_projectList.DockManager = dockManager;
                //_projectList.Name = "ProjectList";
                //_projectList.Show(Dock.Left);
                //(_projectList.ContainerPane as DockingLibrary.DockablePane).AutoHide();

                //_deviceList.Title = (string)Application.Current.FindResource("Menu_Window_Devices");
                //_deviceList.DockManager = dockManager;
                //_deviceList.Name = "DeviceList";
                //_deviceList.Show(Dock.Left);
                //(_deviceList.ContainerPane as DockingLibrary.DockablePane).AutoHide();
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
            //Settings.Default.LayoutString = dockManager.GetLayoutAsXml();
            
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

        //private DockableContent ExtractListWindow(System.Xml.XmlNode type)
        //{
        //    ListWindow lw = new ListWindow();
        //    lw.Title = type.Attributes["Title"].Value;
        //    lw.Name = type.Attributes["Name"].Value;
        //    lw.DockManager = dockManager;
        //    return lw;
        //}
    }
}
