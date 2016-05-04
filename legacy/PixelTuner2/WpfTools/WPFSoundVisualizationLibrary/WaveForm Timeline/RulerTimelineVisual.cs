using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace WPFSoundVisualizationLib
{
    /// <summary>
    /// Visual to place all timestamps and timeticks
    /// </summary>
    public class RulerTimelineVisual : DrawingVisual
    {
        #region Properties

        private readonly Typeface _font = new Typeface("Segoe UI");
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly double _textWidth = 28.0d;
        private Pen _tickPen = new Pen(Brushes.White, 1);
        private double _distanceSinceLastTextStamp = 28.0d;
        private Brush _layoutBrush = new SolidColorBrush(Colors.Black);

        public Brush LayoutBrush
        {
            get
            {
                return _layoutBrush;
            }
            set
            {
                _layoutBrush = value;
                _tickPen = new Pen(_layoutBrush, 1);
            }
        }

        public double LargeTickHeight
        {
            get;
            set;
        }

        public double SmallTickHeight
        {
            get;
            set;
        }

        public Rect? VisibleArea
        {
            get;
            set;
        }

        #endregion

        /// <summary>
        /// Updates layout
        /// </summary>
        /// <param name="step">Distance between time ticks in pixels.</param>
        /// <param name="tickDuration">Seconds between two nearest ticks.</param>
        /// <param name="totalSeconds">Ruler duration in seconds.</param>
        public void Redraw(double step, double tickDuration, double totalSeconds)
        {
            var rulerHeight = Clip.Bounds.Height;

            int startIndex = 0;
            int endIndex = (int)(totalSeconds / tickDuration);
            if (VisibleArea !=null && VisibleArea.HasValue && VisibleArea.Value.Width < Clip.Bounds.Width)
            {
                startIndex = (int)(VisibleArea.Value.Left / step);
                endIndex = (int)(VisibleArea.Value.Right / step);
            }
            if (startIndex == 0) startIndex++;

            using (DrawingContext dc = RenderOpen())
            {
                for (int i = startIndex; i < endIndex; i++)
                {
                    var x = i * step;
                    if (i % 5 == 0)
                    {
                        dc.DrawLine(_tickPen, new Point(x, rulerHeight), new Point(x, rulerHeight - LargeTickHeight));
                        if (_distanceSinceLastTextStamp >= _textWidth)
                        {
                            TimeSpan timeSpan = TimeSpan.FromSeconds(i * tickDuration);
                            var time = tickDuration >= 1 ? string.Format(@"{0:mm\:ss}", timeSpan) + "s" : string.Format(@"{0:mm\:ss\:fff}", timeSpan);
                            var text = new FormattedText(time, _culture, FlowDirection.LeftToRight, _font, 12, LayoutBrush);
                            dc.DrawText(text, new Point(x + 4, rulerHeight - 20));
                            _distanceSinceLastTextStamp = 0;
                        }
                    }
                    else dc.DrawLine(_tickPen, new Point(x, rulerHeight), new Point(x, rulerHeight - SmallTickHeight));
                    _distanceSinceLastTextStamp += step;
                }
            }
        }
    }
}
