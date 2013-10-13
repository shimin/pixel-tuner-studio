using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.ObjectModel;

namespace PixelTuner.ViewModel
{
    public class ProjectsViewModel : ToolViewModel
    {
        public ProjectsViewModel(string name)
            : base(name)
        {
            ContentId = ToolContentId;
            IsVisible = true;
        }

        public const string ToolContentId = "ProjectsTool";

        ObservableCollection<FileViewModel> _files = new ObservableCollection<FileViewModel>();
        ReadOnlyObservableCollection<FileViewModel> _readonlyFiles = null;
        public ReadOnlyObservableCollection<FileViewModel> Files
        {
            get
            {
                if (_readonlyFiles == null)
                    _readonlyFiles = new ReadOnlyObservableCollection<FileViewModel>(_files);

                return _readonlyFiles;
            }
        }

        public void InitializeFilesList(ObservableCollection<FileViewModel> files)
        {
            _files = files;
            _readonlyFiles = new ReadOnlyObservableCollection<FileViewModel>(_files);
        }
    }
}
