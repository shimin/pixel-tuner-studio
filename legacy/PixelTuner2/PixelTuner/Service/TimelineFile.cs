using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PixelTuner.Serialization;

namespace PixelTuner.Service
{
    public class TimelineFile
    {
        public static byte[] EndOfTimelineBlock = new byte[] { 0x50, 0x52, 0xff };

        Dictionary<string, ImageFile> _images = null;
        TimeLineViewModel _viewModel = null;
        public TimelineFile(Dictionary<string, ImageFile> images, TimeLineViewModel tlvm, byte index = 0)
        {
            _images = images;
            _viewModel = tlvm;
            UpdateHeader(index);
        }

        byte _index = 0;

        public byte Index
        {
            get { return _index; }
            set { _index = value; UpdateHeader(value); }
        }

        private void UpdateHeader(byte index)
        {
            _binaryHeader = new List<byte>(Encoding.ASCII.GetBytes("PR"));
            _binaryHeader.Add(index);
            _binaryHeader.Add((byte)_viewModel.Items.Count);
            foreach (var v in _viewModel.Items)
            {
                TimeLineItem item = (TimeLineItem)(v as PixelTuner.ListViewStyle.ThumbnailFileItem);
                string path = item.FullName;
                UInt16 k = 1; //item.K;
                // время в 100мс интервалах
                UInt16 time = (UInt16)(((TimeSpan)(item.EndTime - item.StartTime)).TotalMilliseconds/ 100);
                _binaryHeader.Add(_images[path].Index);
                _binaryHeader.AddRange(BitConverter.GetBytes(k));
                _binaryHeader.AddRange(BitConverter.GetBytes(time));
            }
        }

        List<byte> _binaryHeader = new List<byte>();
        /// <summary>
        /// Заголовок в формате проекта
        /// </summary>
        public List<byte> BinaryHeader
        {
            get { return _binaryHeader; }
            set { _binaryHeader = value; }
        }

    }
}
