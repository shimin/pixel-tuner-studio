using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using Microsoft.Expression.Interactivity.Core;

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

        private int _xOffset;
        public int XOffset
        {
            get { return _xOffset; }
            set { _xOffset = value; RaisePropertyChanged("XOffset"); }
        }

        private int _yOffset;
        public int YOffset
        {
            get { return _yOffset; }
            set { _yOffset = value; RaisePropertyChanged("YOffset"); }
        }

        public VideoThumbnailViewModel()
            : base()
        {
            
        }

        public VideoThumbnailViewModel(FileInfo fi)
            : base(fi)
        {
            OnMediaEndedCommand = new Microsoft.Expression.Interactivity.Core.ActionCommand(OnMediaEnded);
            OnLoadedCommand = new ActionCommand(OnMediaLoaded);
            OnClickCommand = new ActionCommand(OnClick);
            Stretching = 40;
        }

        public override bool CanThumbnail(object parameter)
        {
            return true;
        }

        protected override void OnThumbnail(object parameter)
        {
            ProprietaryEdit(parameter);
        }

        protected override void ProprietaryEdit(object parameter)
        {
            var t = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            if (t == null || t.MetaData == null)
                MessageBox.Show(Application.Current.TryFindResource("Global_PixelElementLinkError") as string);
            else
            {
                var w = new VideoOffsetWindow()
                {
                    Thumbnail = this,
                    CanvasElement = t.MetaData
                };
                if (w.ShowDialog() == true)
                {

                }
            }
        }

        protected override void GenericEdit(object parameter)
        {
            VideoItemWindow w = new VideoItemWindow(this);
            w.Owner = Application.Current.MainWindow;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            //var location = Application.Current.MainWindow.PointToScreen(Mouse.GetPosition(Application.Current.MainWindow));
            //w.Left = location.X - w.Width / 2;
            //w.Top = location.Y - w.Height / 2;
            if (w.ShowDialog() == true)
            {
                ApplyChanges(w.Start, w.Duration, w.Stretching, w.Brightness, w.ForAll, w.DurationChanged, w.StretchingChanged, w.BrightnessChanged);
            }
            if (w.NeedRefresh) RootViewModel.NotifySettingsChanged();
        }

        private void ApplyChanges(DateTime start, TimeSpan duration,
            int stretching, byte brightness, bool forAll, bool durationChanged,
            bool stretchChanged, bool brightnessChanged)
        {
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            if (p != null)
            {
                int idx = p.Items.IndexOf(this) + 1;

                this.StartTime = start > p.Start.Value ? start : p.Start.Value;
                this.EndTime = this.StartTime + duration;
                this.Stretching = stretching;
                this.Brightness = brightness;

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
                    lastUsed = itm.EndTime.Value;
                    idx++;
                }
            }
        }

        [XmlIgnore]
        public ICommand OnMediaEndedCommand { get; set; }

        [XmlIgnore]
        public ICommand OnLoadedCommand { get; set; }

        [XmlIgnore]
        public ICommand OnClickCommand { get; set; }

        private void OnMediaEnded(object obj)
        {
            //var e = (obj as System.Windows.Controls.MediaElement);
            //e.Stop();
            //e.Source = null;
            //e.Source = new Uri(FullName);
            //e.Play();
            //Console.WriteLine("MediaEnded raised.");
        }

        private void OnMediaLoaded(object obj)
        {
            var e = (obj as System.Windows.Controls.MediaElement);
            e.Source = new Uri(FullName);
            e.Play();
            e.Pause();
        }

        private void OnClick(object obj)
        {
            var e = (obj as System.Windows.Controls.MediaElement);
            e.Source = null;
            e.Source = new Uri(FullName);
            e.Play();
        }

        public static implicit operator Serialization.TimeLineItemVideo(VideoThumbnailViewModel item)
        {
            var result = new Serialization.TimeLineItemVideo
            {
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                FullName = item.UseFullName ? item.FullName : Path.Combine("./", item.Name),
                Stretching = item.Stretching,
                Brightness = item.Brightness,
                LaserOn = item.LaserOn,
                XOffset = item.XOffset,
                YOffset = item.YOffset
            };
            return result;
        }

        public override object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(Serialization.TimeLineItem))
            {
                return (Serialization.TimeLineItemVideo)this;
            }
            throw new NotImplementedException();
        }


        protected override void OnReplace(object parameter)
        {
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            var w = new ReplaceVideoItemWindow(new VideosViewModel(
                (String)System.Windows.Application.Current.TryFindResource("Menu_Window_Videos")));

            w.Owner = Application.Current.MainWindow;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (w.ShowDialog() == true)
            { 
                TimeLineItemViewModel newItem = w.SelectedItem as TimeLineItemViewModel;
                if (newItem != null)
                {
                    if (p != null)
                    {
                        RootViewModel.This.ActiveDocument.PushUndo(false);
                        int idx = p.Items.IndexOf(this);
                        newItem.StartTime = this.StartTime;
                        newItem.EndTime = this.EndTime;
                        newItem.Stretching = this.Stretching;
                        (newItem as VideoThumbnailViewModel).XOffset = this.XOffset;
                        (newItem as VideoThumbnailViewModel).YOffset = this.YOffset;
                        p.Items.Remove(this);
                        p.Items.Insert(idx, newItem);
                        p.IsChanged = true;
                    }
                }
            }
        }

        public override void ReplaceWithBlank(int projectHeight = 0)
        {
            BitmapEncoder encoder = new GifBitmapEncoder();
            RenderTargetBitmap bmp = new RenderTargetBitmap(50, projectHeight, 96, 96, PixelFormats.Default);
            
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            var p = Path.GetTempFileName().Replace(".tmp", ".bmp");
            _fi = new FileInfo(p);
            using (Stream stm = File.Create(p))
            {
                encoder.Save(stm);
            }
        }

        public override object Clone(FileInfo fi = null)
        {
            var item = (VideoThumbnailViewModel)base.Clone();
            item.XOffset = XOffset;
            item.YOffset = YOffset;
            return item;
        }
    }
}
