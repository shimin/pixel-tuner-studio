using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace PixelTuner.ViewModel
{
    public class PixelThumbnailViewModel : TimeLineItemViewModel
    {
        public override string ContentID
        {
            get
            {
                return "PixelElement";
            }
        }

        public PixelThumbnailViewModel()
            : base()
        {

        }

        public PixelThumbnailViewModel(FileInfo fi)
            : base(fi)
        {
        }

    }
}
