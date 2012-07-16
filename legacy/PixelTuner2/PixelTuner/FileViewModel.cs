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
        }

        public FileViewModel()
        {
            IsDirty = true;
            Title = FileName;
            _timeLines.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(
               _timeLines_CollectionChanged);
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
            }
        }

        void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged("LineHeightChangingAvailable");
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
            List<byte> projectPrefix = new List<byte>(Encoding.ASCII.GetBytes("FLSH"));
            byte timeLineCount = (byte)TimeLines.Count;
            //UInt32 headerSize = (UInt32)(projectPrefix.Count + 1);
            MemoryStream headerStream = new MemoryStream();
            MemoryStream picStream = new MemoryStream();

            headerStream.Write(projectPrefix.ToArray(), 0, projectPrefix.Count);
            headerStream.WriteByte(timeLineCount);

            // словарь в котором хранятся бинарные представления картинок по ключу - пути к файлу
            Dictionary<string, ImageFile> images = new Dictionary<string, ImageFile>();
            
            // сначала сформирую список вообще всех картинок которые есть в таймлайнах
            foreach (TimeLineViewModel tl in _timeLines)
            {
                foreach (PixelTuner.ListViewStyle.ThumbnailFileItem item in tl.Items)
                {
                    string path = item.FullName;
                    if (!images.ContainsKey(path))
                    {
                        images.Add(path, new ImageFile(path, (byte)images.Count));
                    }
                }
            }

            List<TimelineFile> timelines = new List<TimelineFile>();
            // теперь можно формировать список линий времени
            foreach (TimeLineViewModel tl in _timeLines)
            {
                TimelineFile tlf = new TimelineFile(images, tl);
                timelines.Add(tlf);
                headerStream.Write(tlf.BinaryHeader.ToArray(), 0, tlf.BinaryHeader.Count);
            }
            // конец блока с линиями
            headerStream.Write(TimelineFile.EndOfTimelineBlock, 0, TimelineFile.EndOfTimelineBlock.Length);

            // формирую блок со списком картинок
            foreach (var pair in images)
            {
                headerStream.Write(pair.Value.BinaryHeader.ToArray(), 0, pair.Value.BinaryHeader.Count);
            }
            // конец списка картинок
            headerStream.Write(ImageFile.EndOfImagesBlock, 0, ImageFile.EndOfImagesBlock.Length);

            // адрес выровненный по границе кратной 8
            uint address = (uint)headerStream.Length;
            uint hat = address % 8 != 0 ? (8 - address % 8) : 0;
            address += hat;
            // после формирования заголовка корректирую адреса картинок
            foreach (var pair in images)
            {
                pair.Value.StartAddress = address;
                address += pair.Value.Length;
            }

            // переписываю заголовок
            MemoryStream result = new MemoryStream();
            result.Write(projectPrefix.ToArray(), 0, projectPrefix.Count);
            result.WriteByte(timeLineCount);
            foreach (TimeLineViewModel tl in _timeLines)
            {
                TimelineFile tlf = new TimelineFile(images, tl);
                timelines.Add(tlf);
                result.Write(tlf.BinaryHeader.ToArray(), 0, tlf.BinaryHeader.Count);
            }
            // конец блока с линиями
            result.Write(TimelineFile.EndOfTimelineBlock, 0, TimelineFile.EndOfTimelineBlock.Length);

            // формирую блок со списком картинок
            foreach (var pair in images)
            {
                result.Write(pair.Value.BinaryHeader.ToArray(), 0, pair.Value.BinaryHeader.Count);
            }
            // конец списка картинок
            result.Write(ImageFile.EndOfImagesBlock, 0, ImageFile.EndOfImagesBlock.Length);
            result.Write(new byte[hat], 0, (int)hat);
            foreach (var pair in images)
            {
                result.Write(pair.Value.BinaryData.ToArray(), 0, pair.Value.BinaryData.Count);
            }

            List<byte> resList = new List<byte>(result.GetBuffer());
            try
            {
                using (FileStream fs = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "project.bin"), FileMode.Create))
                {
                    result.WriteTo(fs);
                }
            }
            catch { MessageBox.Show("Не могу заменить файл.", Application.Current.MainWindow.Title); }
            return resList;
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
    }
}
