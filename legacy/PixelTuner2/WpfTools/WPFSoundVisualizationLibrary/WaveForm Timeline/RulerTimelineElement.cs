using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFSoundVisualizationLib
{
    public class RulerTimelineElement : FrameworkElement
    {
        #region Properties

        private VisualCollection _children;

        private ScrollViewer _parentScroll = null;

        public double TotalSeconds
        {
            get;
            set;
        }

        public double TickDuration
        {
            get;
            set;
        }

        public Brush LayoutBrush
        {
            get;
            set;
        }

        #endregion

        public static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            
            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }

        public RulerTimelineElement()
            : base()
        {
            ClipToBounds = true;
            _children = new VisualCollection(this);
            Loaded += RulerTimelineElement_Loaded;
        }

        private void RulerTimelineElement_Loaded(object sender, RoutedEventArgs e)
        {
            _parentScroll = FindParent<ScrollViewer>(this);
            _parentScroll.ScrollChanged += _parentScroll_ScrollChanged;
        }

        private void _parentScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            RulerTimelineVisual _line = null;
            if (_children.Count > 0) _line = _children[0] as RulerTimelineVisual;
            if (_line != null)
            {
                RefreshVisual(_line);
                // update layout:
                if (_children.Count == 2) _children.RemoveAt(1);
                else _children.Add(new DrawingVisual());
            }
        }
        
        public void Initialize(double totalSeconds, double tickDuration)
        {
            TotalSeconds = totalSeconds;
            TickDuration = tickDuration;
            
            RulerTimelineVisual line = null;
            _children.Clear();
            line = new RulerTimelineVisual
            {
                LayoutBrush = this.LayoutBrush,
                SmallTickHeight = 4,
                LargeTickHeight = 12
            };
            RefreshVisual(line);
            _children.Add(line);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            RulerTimelineVisual _line = null;
            if (_children.Count > 0) _line = _children[0] as RulerTimelineVisual;
            if (_line != null) RefreshVisual(_line);

            base.OnRenderSizeChanged(sizeInfo);
        }

        private void RefreshVisual(RulerTimelineVisual _line)
        {
            double minorStepInSeconds = TickDuration / TotalSeconds;
            double minorStepInPoints = minorStepInSeconds * RenderSize.Width;
            _line.Clip = VisualClip;
            if (_parentScroll != null)
            {
                var w = _parentScroll.ViewportWidth;
                var l = _parentScroll.HorizontalOffset;
                _line.VisibleArea = new Rect(Math.Max(0, l - w), 0, 2 * w, VisualClip.Bounds.Height);
            }
            else _line.VisibleArea = null;
            _line.Redraw(minorStepInPoints, TickDuration, TotalSeconds);
        }

        protected override Visual GetVisualChild(int index)
        {
            if (_children.Count == 0) return null;
            return _children[index];
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return 1;
            }
        }

    }
}
