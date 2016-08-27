using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace PixelTuner.Controls.ThumbListView
{
    public partial class ThumbnailEffectsListView : ResourceDictionary
    {
    }

    public partial class EffectView : ViewBase
    {
        #region DefaultStyleKey

        protected override object DefaultStyleKey
        {
            get { return new ComponentResourceKey(GetType(), "EffectView"); }
        }

        #endregion

        #region ItemContainerDefaultStyleKey

        protected override object ItemContainerDefaultStyleKey
        {
            get { return new ComponentResourceKey(GetType(), "EffectListViewItem"); }
        }

        #endregion
    }
}
