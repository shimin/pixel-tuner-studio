using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using System.Windows;
using System.Collections.ObjectModel;
using Core;
using System.Windows.Media;
using System.Xml.Serialization;
using PixelTuner.Properties;
using PixelTuner.Service;
using PixelTuner.Pixelate;

namespace PixelTuner
{
    public class FileViewModel : PaneViewModel
    {
        public FileViewModel(string filePath)
        {
            FilePath = filePath;
            Title = FileName;
            _timeLines.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(
                _timeLines_CollectionChanged);
            DeviceHelper.Devices.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(Devices_CollectionChanged);
        }

        public FileViewModel()
        {
            IsDirty = true;
            Title = FileName;
            _timeLines.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(
               _timeLines_CollectionChanged);
            DeviceHelper.Devices.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(Devices_CollectionChanged);
        }

        #region FilePath
        private string _filePath = null;
        [XmlAttribute]
        public string FilePath
        {
            get { return _filePath; }
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    RaisePropertyChanged("FilePath");
                    RaisePropertyChanged("FileName");
                    RaisePropertyChanged("Title");

                    if (File.Exists(_filePath))
                    {
                        ContentId = _filePath;
                    }
                }
            }
        }
        #endregion

        Brush _darkLineColor = new SolidColorBrush(Color.FromArgb(255, 64, 64, 64));
        Brush _lightLineColor = new SolidColorBrush(Color.FromArgb(255, 70, 70, 70));

        void _timeLines_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (TimeLineViewModel v in e.NewItems)
                {
                    v.Items.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(Items_CollectionChanged);
                }
            for (int i = 0; i < _timeLines.Count; i++)
            {
                if (i % 2 == 0) _timeLines[i].Background =  _lightLineColor;
                else _timeLines[i].Background = _darkLineColor;
                _timeLines[i].Parent = this;
                _timeLines[i].Items.CollectionChanged -= TimeLineItems_CollectionChanged;
                _timeLines[i].Items.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(TimeLineItems_CollectionChanged);
            }
        }

        void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged("LineHeightChangingAvailable");
        }

        void TimeLineItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged("ProjectSize");
        }

        [XmlIgnore]
        public string FileName
        {
            get 
            {
                if (FilePath == null)
                {
                    ResourceDictionary rd = Application.Current.Resources.MergedDictionaries[0];
                    return (String)rd["Doc_Hdr_Default"] + (IsDirty ? " *" : "");
                }

                return System.IO.Path.GetFileName(FilePath) + (IsDirty ? "*" : ""); 
            }
        }

        Double _unitSize = 10.0;
        [XmlAttribute]
        public Double UnitSize
        {
            get { return _unitSize; }
            set { 
                _unitSize = value;
                RaisePropertyChanged("UnitSize");
            }
        }

        int _lineHeight = Settings.Default.LineHeight;
        [XmlAttribute]
        public int LineHeight
        {
            get { return _lineHeight; }
            set 
            { 
                _lineHeight = value;
                Settings.Default.LineHeight = value;
                RaisePropertyChanged("LineHeight"); 
            }
        }

        [XmlIgnore]
        public int ProjectSize
        {
            get
            {
                List<byte> bin = GetBinaryPresentation();
                int size = bin.Count; //(bin.Count / 1024) + 1;
                System.Diagnostics.Debug.WriteLine("Размер проекта: " + size.ToString());
                return size;
                //int result = 32;         // размер заголовка равный 9 взят с запасом.
                //foreach (var t in _timeLines)
                //{
                //    // размер проекта равен произведению числа картинок и размера описателя картинки, 
                //    int size = t.Items.Count * (int)ImageFile.HeaderSize + 1; // плюс байт с количеством картинок
                //    // адрес следующего описателя проекта равен адресу текущего, плюс длина текущего
                //    result += size;
                //}
                //return result;
            }
        }

        [XmlIgnore]
        public bool LineHeightChangingAvailable
        {
            get
            {
                foreach (var line in TimeLines)
                    if (line.Items.Count > 0)
                        return false;
                return true;
            }
        }

        [XmlIgnore]
        public override Uri IconSource
        {
            get
            {
                return new Uri("/PixelTuner;component/Images/document.png", UriKind.RelativeOrAbsolute);
            }
        }

        public override string ToString()
        {
            return String.Format("{0} < {1} >", this.FileName, this.FilePath);
        }

        #region IsDirty

        private bool _isDirty = false;
        [XmlIgnore]
        public bool IsDirty
        {
            get { return _isDirty; }
            set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    RaisePropertyChanged("IsDirty");
                    RaisePropertyChanged("FileName");
                }
            }
        }

        #endregion

        #region SaveCommand
        RelayCommand _saveCommand = null;
        public ICommand SaveCommand
        {
            get
            {
                if (_saveCommand == null)
                {
                    _saveCommand = new RelayCommand((p) => OnSave(p), (p) => CanSave(p));
                }

                return _saveCommand;
            }
        }

        private bool CanSave(object parameter)
        {
            return IsDirty;
        }

        private void OnSave(object parameter)
        {
            RootViewModel.This.Save(this, false);
        }

        #endregion

        #region SaveAsCommand
        RelayCommand _saveAsCommand = null;
        public ICommand SaveAsCommand
        {
            get
            {
                if (_saveAsCommand == null)
                {
                    _saveAsCommand = new RelayCommand((p) => OnSaveAs(p), (p) => CanSaveAs(p));
                }

                return _saveAsCommand;
            }
        }

        private bool CanSaveAs(object parameter)
        {
            return IsDirty;
        }

        private void OnSaveAs(object parameter)
        {
            RootViewModel.This.Save(this, true);
        }

        #endregion

        #region CloseCommand
        RelayCommand _closeCommand = null;
        public ICommand CloseCommand
        {
            get
            {
                if (_closeCommand == null)
                {
                    _closeCommand = new RelayCommand((p) => OnClose(), (p) => CanClose());
                }

                return _closeCommand;
            }
        }

        private bool CanClose()
        {
            return true;
        }

        private void OnClose()
        {
            RootViewModel.This.Close(this);
        }
        #endregion

        #region AddLineCommand
        RelayCommand _addLineCommand = null;
        public ICommand AddLine
        {
            get
            {
                if (_addLineCommand == null)
                {
                    _addLineCommand = new RelayCommand((p) => OnAddLine(p), (p) => CanAddLine(p));
                }

                return _addLineCommand;
            }
        }

        private bool CanAddLine(object parameter)
        {
            return TimeLines.Count < 254;
        }

        private void OnAddLine(object parameter)
        {
            Console.WriteLine("ButtonPressed");
            _timeLines.Add(new TimeLineViewModel());
        }

        #endregion 
        
        public List<byte> GetBinaryPresentation()
        {
            return GetBinaryPresentation(this.LineHeight);
        }

        public List<byte> GetBinaryPresentation(int targetHeight)
        {
            var knownPaths = new List<string>();
            byte projectHeight = (byte)targetHeight;

            PixelProjectFile project = new PixelProjectFile();
            foreach (TimeLineViewModel tl in _timeLines)
            {
                if (tl.Items.Count == 0) continue;
                var tlf = new PixelTimelineFile();

                foreach (PixelTuner.ListViewStyle.ThumbnailFileItem item in tl.Items)
                {
                    string path = item.FullName.ToLower();
                    UInt16 duration = (UInt16)(((TimeSpan)(item.EndTime - item.StartTime)).TotalMilliseconds / 100);                    
                    
                    var imgf = new PixelImageFile(path, (UInt16)item.Stretching, duration);
                    knownPaths.Add(path);
                    tlf.Images.Add(imgf);
                }
                project.TimeLines.Add(tlf);
            }

            ImagesCollection c = new ImagesCollection(knownPaths, projectHeight);
            return project.GetBinary(c, projectHeight);
        }

        [XmlIgnore]
        public static List<Type> ExtraTypes
        {
            get
            {
                var result = new List<Type>()
                {
                    typeof(TimeLineViewModel)
                };
                result.AddRange(TimeLineViewModel.ExtraTypes);
                return result;
            }
        }

        public int MemoryListHeight
        {
            get
            {
                return DeviceHelper.Devices.Count != 0 ? DeviceHelper.Devices.Count * 15 + 20 : 0;
            }
        }

        public System.Windows.Visibility Visibility
        {
            get
            {
                return DeviceHelper.Devices.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        void Devices_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged("MemoryListHeight");
            RaisePropertyChanged("Visibility");
        }

        ObservableCollection<TimeLineViewModel> _timeLines = new ObservableCollection<TimeLineViewModel>();
        [XmlElement(ElementName = "Line")]
        public ObservableCollection<TimeLineViewModel> TimeLines
        {
            get
            {
                if (_timeLines == null)
                    _timeLines = new ObservableCollection<TimeLineViewModel>(_timeLines);

                 //if (_timeLines.Count == 0) _timeLines.Add(new TimeLineViewModel());
                return _timeLines;
            }
            set
            {
                _timeLines = value;
            }
        }

        public object tlf { get; set; }
    }
}
