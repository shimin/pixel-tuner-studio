using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace PixelTuner.Import
{
    public class HistoryEntry
    {
        Bitmap _image = null;
        public Bitmap Image
        {
            get { return _image; }
            private set { _image = value; }
        }

        string _note = "";
        public string Note
        {
            get { return _note; }
            private set { _note = value; }
        }

        public HistoryEntry(Bitmap image, string note)
        {
            _image = image;
            _note = note;
        }

        public override string ToString()
        {
            return Note;
        }
    }
}
