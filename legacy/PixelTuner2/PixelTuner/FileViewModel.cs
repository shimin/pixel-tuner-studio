using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using System.Windows;
using System.Collections.ObjectModel;
using TimeLineTool;
using System.Windows.Media;
using System.Xml.Serialization;
using PixelTuner.Properties;

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
        [XmlIgnore]
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
                ResourceDictionary rd = Application.Current.Resources.MergedDictionaries[0];
                if (FilePath == null)
                    return (String)rd["Doc_Hdr_Default"] + (IsDirty ? " *" : "");

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
            Workspace.This.Save(this, false);
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
            Workspace.This.Save(this, true);
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
            Workspace.This.Close(this);
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
            return true;
        }

        private void OnAddLine(object parameter)
        {
            Console.WriteLine("ButtonPressed");
            _timeLines.Add(new TimeLineViewModel());
        }

        #endregion 

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
