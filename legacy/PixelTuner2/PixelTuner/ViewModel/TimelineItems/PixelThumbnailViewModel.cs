using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Input;

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

        public override bool CanRepeat(object parameter)
        {
            return true;
        }

        public override bool CanThumbnail(object parameter)
        {
            return base.CanThumbnail(parameter);
        }

        public override bool CanSwitchLaserOn(object parameter)
        {
            return true;
        }

        protected override void OnSwitchLaserOn(object parameter)
        {
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            if (p != null) { RootViewModel.This.ActiveDocument.PushUndo(false, p); }

            LaserOn = !LaserOn;
            RaisePropertyChanged("LaserOn");
            RaisePropertyChanged("LaserOnButtonColor");
        }

        protected override void GenericEdit(object parameter)
        {
            ItemWindow w = new ItemWindow(this);
            var location = Application.Current.MainWindow.PointToScreen(Mouse.GetPosition(Application.Current.MainWindow));
            w.Left = location.X - w.Width / 2;
            w.Top = location.Y - w.Height / 2;
            if (w.ShowDialog() == true)
            {
                ApplyChanges(w.Start, w.Duration, w.Stretching, w.Brightness, w.LaserOn, w.ForAll, w.DurationChanged, w.StretchingChanged, w.BrightnessChanged, w.LaserOnChanged);
            }
            if (w.NeedRefresh) RootViewModel.NotifySettingsChanged();
        }

        private void ApplyChanges(DateTime start, TimeSpan duration,
            int stretching, byte brightness, bool laserOn, bool forAll, bool durationChanged,
            bool stretchChanged, bool brightnessChanged, bool laserOnChanged)
        {
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            if (p != null)
            {
                int idx = p.Items.IndexOf(this) + 1;

                this.StartTime = start > p.Start.Value ? start : p.Start.Value;
                this.EndTime = this.StartTime + duration;
                this.Stretching = stretching;
                this.Brightness = brightness;
                this.LaserOn = laserOn;

                var thisIdx = p.Items.IndexOf(this);
                if (thisIdx < p.Items.Count - 1 && !forAll && this.EndTime.Value > p.Items[thisIdx + 1].StartTime.Value)
                    if (PixelTuner.Properties.Settings.Default.CutNextItems)
                    {
                        p.CutItemsWithinInterval(p.Items[thisIdx + 1].StartTime, this.EndTime);
                    }
                    else
                    {
                        TimeSpan moveInterval = EndTime.Value - p.Items[thisIdx + 1].StartTime.Value;
                        p.MoveItemsAfterIndex(p.Items[thisIdx + 1].StartTime, moveInterval);
                    }

                DateTime lastUsed = this.EndTime.Value;
                while (forAll && idx < p.Items.Count)
                {
                    var itm = (p.Items[idx] as TimeLineItemViewModel);
                    if (durationChanged)
                    {
                        if (p.Items[idx].StartTime.Value < lastUsed) p.Items[idx].StartTime = lastUsed;
                        p.Items[idx].EndTime = p.Items[idx].StartTime.Value.Add(duration);
                    }
                    if (stretchChanged) itm.Stretching = stretching;
                    if (brightnessChanged) itm.Brightness = brightness;
                    if (laserOnChanged) itm.LaserOn = laserOn;
                    lastUsed = itm.EndTime.Value;
                    idx++;
                }
            }
        }
    }
}
