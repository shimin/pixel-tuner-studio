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
        public TimelineFile(Dictionary<string, ImageFile> images, TimeLineViewModel tlvm)
        {
            _images = images;
            _viewModel = tlvm;
            UpdateHeader();
        }

        private void UpdateHeader()
        {
            _binaryHeader = new List<byte>();// (Encoding.ASCII.GetBytes("PR"));
            //_binaryHeader.Add(index);
            _binaryHeader.Add((byte)_viewModel.Items.Count);
            foreach (var v in _viewModel.Items)
            {
                TimeLineItem item = (TimeLineItem)(v as PixelTuner.ListViewStyle.ThumbnailFileItem);
                string path = item.FullName.ToLower();
                UInt16 k = (UInt16)item.Stretching;
                // время в 100мс интервалах
                UInt16 time = (UInt16)(((TimeSpan)(item.EndTime - item.StartTime)).TotalMilliseconds/ 100);
                _binaryHeader.AddRange(BitConverter.GetBytes(_images[path].StartAddress));      // 4 байта
                _binaryHeader.AddRange(BitConverter.GetBytes(k));                               // 2 байта
                _binaryHeader.AddRange(BitConverter.GetBytes(time));                            // 2 байта
            }
        }

        List<byte> _binaryHeader = new List<byte>();
        /// <summary>
        /// Заголовок в формате проекта
        /// </summary>
        public List<byte> BinaryHeader
        {
            get { return _binaryHeader; }
            private set { _binaryHeader = value; }
        }

    }
}
