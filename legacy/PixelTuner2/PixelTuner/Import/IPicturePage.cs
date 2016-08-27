using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;

namespace PixelTuner.Import
{
    interface IPicturePage
    {
        void ApplyChanges();
        bool IsPreviewOn { get; set; }
        void SetSource(BitmapSource bmp);
        BitmapSource Result { get; }
    }
}
