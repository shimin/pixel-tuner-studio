using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Input;
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
                FullName = item.FullName,
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
                        RootViewModel.This.ActiveDocument.PushUndo(false, p);
                        int idx = p.Items.IndexOf(this);
                        newItem.StartTime = this.StartTime;
                        newItem.EndTime = this.EndTime;
                        newItem.Stretching = this.Stretching;
                        p.Items.Remove(this);
                        p.Items.Insert(idx, newItem);
                        p.IsChanged = true;
                    }
                }
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
