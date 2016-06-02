using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixelTuner.ViewModel
{
    public class UndoRedoEventArgs : EventArgs
    {
        public UndoRedoEventArgs(object sender, TimeLineViewModel lineBeforeChanges)
        {

        }
    }
}
