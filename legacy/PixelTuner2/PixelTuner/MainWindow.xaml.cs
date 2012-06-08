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
using AvalonDock.Layout.Serialization;
using System.IO;
using System.Collections.ObjectModel;
using TimeLineTool;

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
            this.DataContext = Workspace.This;
            //TimeLineInit();

        }

        private void Window_Loaded(object sender, RoutedEventArgs ev)
        {
            //dockManager.ParentWindow = this;

#if (DONT_SAVE_LAYOUT)
#else
            if (!string.IsNullOrEmpty(_layoutString))
            {
                var layoutSerializer = new XmlLayoutSerializer(dockManager);
                using (StringReader sr = new StringReader(_layoutString))
                {
                    layoutSerializer.LayoutSerializationCallback += (s, args) =>
                    {
                        args.Content = Application.Current.MainWindow.FindName(args.Model.ContentId);
                    };
                    layoutSerializer.Deserialize(sr);
                }
            }
#endif

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
            var layoutSerializer = new XmlLayoutSerializer(dockManager);
            StringBuilder sb = new StringBuilder();
            using (StringWriter sw = new StringWriter(sb))
            {
                layoutSerializer.Serialize(sw);
            }
            Settings.Default.LayoutString = sb.ToString();
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

        private void Slider_Scale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //TimeLine3.UnitSize = Slider_Scale.Value;
            //TimeLine2.UnitSize = Slider_Scale.Value;
        }

        #region TimeLineTool

        ObservableCollection<ITimeLineDataItem> data = new ObservableCollection<ITimeLineDataItem>();
        public ObservableCollection<ITimeLineDataItem> t2Data = new ObservableCollection<ITimeLineDataItem>();
        public ObservableCollection<ITimeLineDataItem> t3Data = new ObservableCollection<ITimeLineDataItem>();
        //ObservableCollection<ITimeLineDataItem> listboxData = new ObservableCollection<ITimeLineDataItem>();

        public void TimeLineInit()
        {
            //var tmp1 = new TempDataType()
            //{
            //    StartTime = DateTime.MinValue,
            //    EndTime = DateTime.MinValue.AddHours(18),
            //    Name = "Temp 1"
            //};
            //var tmp2 = new TempDataType()
            //{
            //    StartTime = DateTime.MinValue,
            //    EndTime = DateTime.Now.AddHours(33),
            //    Name = "Temp 2"
            //};
            //var temp3 = new TempDataType()
            //{
            //    StartTime = DateTime.Now.AddHours(44),
            //    EndTime = DateTime.Now.AddHours(60),
            //    Name = "Temp 3"
            //};
            //var temp4 = new TempDataType()
            //{
            //    StartTime = DateTime.Now.AddHours(60),
            //    EndTime = DateTime.Now.AddHours(70),
            //    Name = "Temp 4"
            //};

            //data.Add(tmp1);
            //data.Add(tmp2);
            //data.Add(temp3);
            //data.Add(temp4);

            //t2Data.Add(tmp1);
            //t3Data.Add(temp3);

            //TimeLine2.Items = data;
            //TimeLine2.StartDate = DateTime.Now;

            //TimeLine3.StartDate = DateTime.Now;
            //TimeLine2.Items = t2Data;
            //TimeLine3.Items = t3Data;

            //var lb1 = new TempDataType()
            //{
            //    Name = "Экземпляр 1"
            //};
            //var lb2 = new TempDataType()
            //{
            //    Name = "Экземпляр 2"
            //};
            //var lb3 = new TempDataType()
            //{
            //    Name = "Экземпляр 3"
            //};
            //var lb4 = new TempDataType()
            //{
            //    Name = "Экземпляр 4"
            //};
            //listboxData.Add(lb1);
            //listboxData.Add(lb2);
            //listboxData.Add(lb3);
            //listboxData.Add(lb4);
            //ListSrc.ItemsSource = listboxData;
            //Slider_Scale.Value = 25;
        }

        #endregion
    }
}
