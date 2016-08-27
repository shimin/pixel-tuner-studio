using PixelTuner.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixelTuner.UndoRedo
{
    public enum UserAction
    {
        TimelineAdd,
        TimelineDelete,
        TimelineClone,
        TimelineMove,
        ItemAdd,
        ItemDelete,
        ItemMove,
        ItemChange,
    }

    public class StateManager
    {
        Stack<State> _undoStack = new Stack<State>();
        Stack<State> _redoStack = new Stack<State>();
        FileViewModel _file = null;

        public StateManager(FileViewModel fileToOperate)
        {
            _file = fileToOperate;
        }

        public void BeforeStateChanged(State current)
        {

        }

        public void Undo()
        {

        }

        public void Redo()
        {

        }

        private void SetState(State s)
        {

        }
    }
}
