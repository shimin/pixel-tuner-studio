using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PixelTuner.ViewModel
{
    public class VideoThumbnailViewModel : TimeLineItemViewModel
    {
        public override string ContentID
        {
            get
            {
                return "VideoElement";
            }
        }

        public VideoThumbnailViewModel()
            : base()
        {

        }

        public VideoThumbnailViewModel(FileInfo fi)
            : base(fi)
        {
        }
    }
}
