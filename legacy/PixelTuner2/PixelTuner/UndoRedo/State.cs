using PixelTuner.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixelTuner.UndoRedo
{
    public class State
    {
        ObservableCollection<TimeLineViewModel> _timelines = new ObservableCollection<TimeLineViewModel>();

        public State(IList<TimeLineViewModel> changingLines, UserAction beforeAction)
        {

        }
    }
}
