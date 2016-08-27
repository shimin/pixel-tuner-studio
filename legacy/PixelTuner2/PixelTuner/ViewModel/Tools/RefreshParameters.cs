using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PixelTuner.ViewModel
{
    internal class RefreshParameters
    {
        int _height = 0;
        public int Height
        {
            get { return _height; }
        }
        string _itemToRemove = "";
        public string ItemToRemove
        {
            get { return _itemToRemove; }
        }
        string _filter = "";
        public string Filter
        {
            get { return _filter; }
        }
        public RefreshParameters(int height = 0, string itemToRemove = "", string filter = "")
        {
            _height = height;
            _itemToRemove = itemToRemove;
            _filter = filter;
        }
    }

}
