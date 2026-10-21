// Copyright (C) 2011 - 2012, Jacob Johnston 
//
// Permission is hereby granted, free of charge, to any person obtaining a 
// copy of this software and associated documentation files (the "Software"), 
// to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, 
// and/or sell copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following conditions: 
//
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software. 
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE. 

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WPFSoundVisualizationLib
{
    /// <summary>
    /// A control that displays a stereo waveform and
    /// allows a user to change playback position.
    /// </summary>
    [DisplayName("Waveform Timeline")]
    [Description("Displays a stereo waveform and allows a user to change playback position.")]
    [ToolboxItem(true)]
    [TemplatePart(Name = "PART_Waveform", Type = typeof(Canvas)),
     TemplatePart(Name = "PART_Timeline", Type = typeof(RulerTimelineElement)),
     TemplatePart(Name = "PART_Repeat", Type = typeof(Canvas)),
     TemplatePart(Name = "PART_Progress", Type = typeof(Canvas))]
    public class WaveformTimeline : Control
    {
        #region Fields
        private IWaveformPlayer soundPlayer;
        private Canvas waveformCanvas;
        private Canvas repeatCanvas;
        private RulerTimelineElement timelineCanvas;
        private Canvas progressCanvas;
        private readonly Path leftPath = new Path();
        private readonly Path rightPath = new Path();
        private readonly Line centerLine = new Line();
        private readonly Rectangle repeatRegion = new Rectangle();
        private readonly Line progressLine = new Line();
        private readonly Path progressIndicator = new Path();
        private readonly double VerticalScale = 1.2d;
        private double ChannelLength = double.NaN;
        private bool isMouseDown;
        private bool isMouseScaling;
        private Point mouseScalingPoint;
        private Point mouseDownPoint;
        private Point currentPoint;
        private double startLoopRegion = -1;
        private double endLoopRegion = -1;
        private Size _initialSize = Size.Empty; 
        #endregion

        #region Constants
        private const int mouseMoveTolerance = 3;
        private const int indicatorTriangleWidth = 4;
        #endregion

        #region Dependency Properties
        #region LeftLevelBrush
        /// <summary>
        /// Identifies the <see cref="LeftLevelBrush" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty LeftLevelBrushProperty = DependencyProperty.Register("LeftLevelBrush", typeof(Brush), typeof(WaveformTimeline), new UIPropertyMetadata(new SolidColorBrush(Colors.Blue), OnLeftLevelBrushChanged, OnCoerceLeftLevelBrush));

        private static object OnCoerceLeftLevelBrush(DependencyObject o, object value)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                return waveformTimeline.OnCoerceLeftLevelBrush((Brush)value);
            else
                return value;
        }

        private static void OnLeftLevelBrushChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                waveformTimeline.OnLeftLevelBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
        }

        /// <summary>
        /// Coerces the value of <see cref="LeftLevelBrush"/> when a new value is applied.
        /// </summary>
        /// <param name="value">The value that was set on <see cref="LeftLevelBrush"/></param>
        /// <returns>The adjusted value of <see cref="LeftLevelBrush"/></returns>
        protected virtual Brush OnCoerceLeftLevelBrush(Brush value)
        {
            return value;
        }

        /// <summary>
        /// Called after the <see cref="LeftLevelBrush"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="LeftLevelBrush"/></param>
        /// <param name="newValue">The new value of <see cref="LeftLevelBrush"/></param>
        protected virtual void OnLeftLevelBrushChanged(Brush oldValue, Brush newValue)
        {
            leftPath.Fill = LeftLevelBrush;
            GenerateWaveform();
        }

        /// <summary>
        /// Gets or sets a brush used to draw the left channel output on the waveform.
        /// </summary>        
        [Category("Brushes")]
        public Brush LeftLevelBrush
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (Brush)GetValue(LeftLevelBrushProperty);
            }
            set
            {
                SetValue(LeftLevelBrushProperty, value);
            }
        }
        #endregion

        #region RightLevelBrush
        /// <summary>
        /// Identifies the <see cref="RightLevelBrush" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty RightLevelBrushProperty = DependencyProperty.Register("RightLevelBrush", typeof(Brush), typeof(WaveformTimeline), new UIPropertyMetadata(new SolidColorBrush(Colors.Red), OnRightLevelBrushChanged, OnCoerceRightLevelBrush));

        private static object OnCoerceRightLevelBrush(DependencyObject o, object value)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                return waveformTimeline.OnCoerceRightLevelBrush((Brush)value);
            else
                return value;
        }

        private static void OnRightLevelBrushChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                waveformTimeline.OnRightLevelBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
        }

        /// <summary>
        /// Coerces the value of <see cref="RightLevelBrush"/> when a new value is applied.
        /// </summary>
        /// <param name="value">The value that was set on <see cref="RightLevelBrush"/></param>
        /// <returns>The adjusted value of <see cref="RightLevelBrush"/></returns>
        protected virtual Brush OnCoerceRightLevelBrush(Brush value)
        {
            return value;
        }

        /// <summary>
        /// Called after the <see cref="RightLevelBrush"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="RightLevelBrush"/></param>
        /// <param name="newValue">The new value of <see cref="RightLevelBrush"/></param>
        protected virtual void OnRightLevelBrushChanged(Brush oldValue, Brush newValue)
        {
            rightPath.Fill = RightLevelBrush;
            GenerateWaveform();
        }

        /// <summary>
        /// Gets or sets a brush used to draw the right speaker levels on the waveform.
        /// </summary>
        [Category("Brushes")]
        public Brush RightLevelBrush
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (Brush)GetValue(RightLevelBrushProperty);
            }
            set
            {
                SetValue(RightLevelBrushProperty, value);
            }
        }
        #endregion

        #region ProgressBarBrush
        /// <summary>
        /// Identifies the <see cref="ProgressBarBrush" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty ProgressBarBrushProperty = DependencyProperty.Register("ProgressBarBrush", typeof(Brush), typeof(WaveformTimeline), new UIPropertyMetadata(new SolidColorBrush(Color.FromArgb(0xCD, 0xBA, 0x00, 0xFF)), OnProgressBarBrushChanged, OnCoerceProgressBarBrush));

        private static object OnCoerceProgressBarBrush(DependencyObject o, object value)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                return waveformTimeline.OnCoerceProgressBarBrush((Brush)value);
            else
                return value;
        }

        private static void OnProgressBarBrushChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                waveformTimeline.OnProgressBarBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
        }

        /// <summary>
        /// Coerces the value of <see cref="ProgressBarBrush"/> when a new value is applied.
        /// </summary>
        /// <param name="value">The value that was set on <see cref="ProgressBarBrush"/></param>
        /// <returns>The adjusted value of <see cref="ProgressBarBrush"/></returns>
        protected virtual Brush OnCoerceProgressBarBrush(Brush value)
        {
            return value;
        }

        /// <summary>
        /// Called after the <see cref="ProgressBarBrush"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="ProgressBarBrush"/></param>
        /// <param name="newValue">The new value of <see cref="ProgressBarBrush"/></param>
        protected virtual void OnProgressBarBrushChanged(Brush oldValue, Brush newValue)
        {
            progressIndicator.Fill = ProgressBarBrush;
            progressLine.Stroke = ProgressBarBrush;

            CreateProgressIndicator();
        }

        /// <summary>
        /// Gets or sets a brush used to draw the track progress indicator bar.
        /// </summary>
        [Category("Brushes")]
        public Brush ProgressBarBrush
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (Brush)GetValue(ProgressBarBrushProperty);
            }
            set
            {
                SetValue(ProgressBarBrushProperty, value);
            }
        }
        #endregion
        
        #region ProgressBarThickness
        /// <summary>
        /// Identifies the <see cref="ProgressBarThickness" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty ProgressBarThicknessProperty = DependencyProperty.Register("ProgressBarThickness", typeof(double), typeof(WaveformTimeline), new UIPropertyMetadata(2.0d, OnProgressBarThicknessChanged, OnCoerceProgressBarThickness));

        private static object OnCoerceProgressBarThickness(DependencyObject o, object value)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                return waveformTimeline.OnCoerceProgressBarThickness((double)value);
            else
                return value;
        }

        private static void OnProgressBarThicknessChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                waveformTimeline.OnProgressBarThicknessChanged((double)e.OldValue, (double)e.NewValue);
        }

        /// <summary>
        /// Coerces the value of <see cref="ProgressBarThickness"/> when a new value is applied.
        /// </summary>
        /// <param name="value">The value that was set on <see cref="ProgressBarThickness"/></param>
        /// <returns>The adjusted value of <see cref="ProgressBarThickness"/></returns>
        protected virtual double OnCoerceProgressBarThickness(double value)
        {
            value = Math.Max(value, 0.0d);
            return value;
        }

        /// <summary>
        /// Called after the <see cref="ProgressBarThickness"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="ProgressBarThickness"/></param>
        /// <param name="newValue">The new value of <see cref="ProgressBarThickness"/></param>
        protected virtual void OnProgressBarThicknessChanged(double oldValue, double newValue)
        {
            progressLine.StrokeThickness = ProgressBarThickness;
            CreateProgressIndicator();
        }

        /// <summary>
        /// Get or sets the thickness of the progress indicator bar.
        /// </summary>
        [Category("Common")]
        public double ProgressBarThickness
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (double)GetValue(ProgressBarThicknessProperty);
            }
            set
            {
                SetValue(ProgressBarThicknessProperty, value);
            }
        }
        #endregion
        
        #region CenterLineBrush
        /// <summary>
        /// Identifies the <see cref="CenterLineBrush" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty CenterLineBrushProperty = DependencyProperty.Register("CenterLineBrush", typeof(Brush), typeof(WaveformTimeline), new UIPropertyMetadata(new SolidColorBrush(Colors.Black), OnCenterLineBrushChanged, OnCoerceCenterLineBrush));

        private static object OnCoerceCenterLineBrush(DependencyObject o, object value)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                return waveformTimeline.OnCoerceCenterLineBrush((Brush)value);
            else
                return value;
        }

        private static void OnCenterLineBrushChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                waveformTimeline.OnCenterLineBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
        }

        /// <summary>
        /// Coerces the value of <see cref="CenterLineBrush"/> when a new value is applied.
        /// </summary>
        /// <param name="value">The value that was set on <see cref="CenterLineBrush"/></param>
        /// <returns>The adjusted value of <see cref="CenterLineBrush"/></returns>
        protected virtual Brush OnCoerceCenterLineBrush(Brush value)
        {
            return value;
        }

        /// <summary>
        /// Called after the <see cref="CenterLineBrush"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="CenterLineBrush"/></param>
        /// <param name="newValue">The new value of <see cref="CenterLineBrush"/></param>
        protected virtual void OnCenterLineBrushChanged(Brush oldValue, Brush newValue)
        {
            centerLine.Stroke = CenterLineBrush;
            GenerateWaveform();
        }

        /// <summary>
        /// Gets or sets a brush used to draw the center line separating left and right levels.
        /// </summary>
        [Category("Brushes")]
        public Brush CenterLineBrush
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (Brush)GetValue(CenterLineBrushProperty);
            }
            set
            {
                SetValue(CenterLineBrushProperty, value);
            }
        }
        #endregion

        #region CenterLineThickness
        /// <summary>
        /// Identifies the <see cref="CenterLineThickness" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty CenterLineThicknessProperty = DependencyProperty.Register("CenterLineThickness", typeof(double), typeof(WaveformTimeline), new UIPropertyMetadata(1.0d, OnCenterLineThicknessChanged, OnCoerceCenterLineThickness));

        private static object OnCoerceCenterLineThickness(DependencyObject o, object value)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                return waveformTimeline.OnCoerceCenterLineThickness((double)value);
            else
                return value;
        }

        private static void OnCenterLineThicknessChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                waveformTimeline.OnCenterLineThicknessChanged((double)e.OldValue, (double)e.NewValue);
        }

        /// <summary>
        /// Coerces the value of <see cref="CenterLineThickness"/> when a new value is applied.
        /// </summary>
        /// <param name="value">The value that was set on <see cref="CenterLineThickness"/></param>
        /// <returns>The adjusted value of <see cref="CenterLineThickness"/></returns>
        protected virtual double OnCoerceCenterLineThickness(double value)
        {
            value = Math.Max(value, 0.0d);
            return value;
        }

        /// <summary>
        /// Called after the <see cref="CenterLineThickness"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="CenterLineThickness"/></param>
        /// <param name="newValue">The new value of <see cref="CenterLineThickness"/></param>
        protected virtual void OnCenterLineThicknessChanged(double oldValue, double newValue)
        {
            centerLine.StrokeThickness = CenterLineThickness;
            GenerateWaveform();
        }

        /// <summary>
        /// Gets or sets the thickness of the center line separating left and right levels.
        /// </summary>
        [Category("Common")]
        public double CenterLineThickness
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (double)GetValue(CenterLineThicknessProperty);
            }
            set
            {
                SetValue(CenterLineThicknessProperty, value);
            }
        }
        #endregion

        #region RepeatRegionBrush
        /// <summary>
        /// Identifies the <see cref="RepeatRegionBrush" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty RepeatRegionBrushProperty = DependencyProperty.Register("RepeatRegionBrush", typeof(Brush), typeof(WaveformTimeline), new UIPropertyMetadata(new SolidColorBrush(Color.FromArgb(0x81, 0xF6, 0xFF, 0x00)), OnRepeatRegionBrushChanged, OnCoerceRepeatRegionBrush));

        private static object OnCoerceRepeatRegionBrush(DependencyObject o, object value)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                return waveformTimeline.OnCoerceRepeatRegionBrush((Brush)value);
            else
                return value;
        }

        private static void OnRepeatRegionBrushChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                waveformTimeline.OnRepeatRegionBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
        }

        /// <summary>
        /// Coerces the value of <see cref="RepeatRegionBrush"/> when a new value is applied.
        /// </summary>
        /// <param name="value">The value that was set on <see cref="RepeatRegionBrush"/></param>
        /// <returns>The adjusted value of <see cref="RepeatRegionBrush"/></returns>
        protected virtual Brush OnCoerceRepeatRegionBrush(Brush value)
        {
            return value;
        }

        /// <summary>
        /// Called after the <see cref="RepeatRegionBrush"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="RepeatRegionBrush"/></param>
        /// <param name="newValue">The new value of <see cref="RepeatRegionBrush"/></param>
        protected virtual void OnRepeatRegionBrushChanged(Brush oldValue, Brush newValue)
        {
            repeatRegion.Fill = RepeatRegionBrush;
            UpdateRepeatRegion();
        }

        /// <summary>
        /// Gets or sets a brush used to draw the repeat region on the waveform.
        /// </summary>
        [Category("Brushes")]
        public Brush RepeatRegionBrush
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (Brush)GetValue(RepeatRegionBrushProperty);
            }
            set
            {
                SetValue(RepeatRegionBrushProperty, value);
            }
        }

        #endregion

        #region AllowRepeatRegions
        /// <summary>
        /// Identifies the <see cref="AllowRepeatRegions" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty AllowRepeatRegionsProperty = DependencyProperty.Register("AllowRepeatRegions", typeof(bool), typeof(WaveformTimeline), new UIPropertyMetadata(true, OnAllowRepeatRegionsChanged, OnCoerceAllowRepeatRegions));

        private static object OnCoerceAllowRepeatRegions(DependencyObject o, object value)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                return waveformTimeline.OnCoerceAllowRepeatRegions((bool)value);
            else
                return value;
        }

        private static void OnAllowRepeatRegionsChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                waveformTimeline.OnAllowRepeatRegionsChanged((bool)e.OldValue, (bool)e.NewValue);
        }

        /// <summary>
        /// Coerces the value of <see cref="AllowRepeatRegions"/> when a new value is applied.
        /// </summary>
        /// <param name="value">The value that was set on <see cref="AllowRepeatRegions"/></param>
        /// <returns>The adjusted value of <see cref="AllowRepeatRegions"/></returns>
        protected virtual bool OnCoerceAllowRepeatRegions(bool value)
        {
            return value;
        }

        /// <summary>
        /// Called after the <see cref="AllowRepeatRegions"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="AllowRepeatRegions"/></param>
        /// <param name="newValue">The new value of <see cref="AllowRepeatRegions"/></param>
        protected virtual void OnAllowRepeatRegionsChanged(bool oldValue, bool newValue)
        {
            if (!newValue && soundPlayer != null)
            {
                soundPlayer.SelectionBegin = TimeSpan.Zero;
                soundPlayer.SelectionEnd = TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether repeat regions will be created via mouse drag across the waveform.
        /// </summary>
        [Category("Common")]
        public bool AllowRepeatRegions
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (bool)GetValue(AllowRepeatRegionsProperty);
            }
            set
            {
                SetValue(AllowRepeatRegionsProperty, value);
            }
        }
        #endregion
        
        #region TimelineTickBrush
        /// <summary>
        /// Identifies the <see cref="TimelineTickBrush" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty TimelineTickBrushProperty = DependencyProperty.Register("TimelineTickBrush", typeof(Brush), typeof(WaveformTimeline), new UIPropertyMetadata(new SolidColorBrush(Colors.Black), OnTimelineTickBrushChanged, OnCoerceTimelineTickBrush));

        private static object OnCoerceTimelineTickBrush(DependencyObject o, object value)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                return waveformTimeline.OnCoerceTimelineTickBrush((Brush)value);
            else
                return value;
        }

        private static void OnTimelineTickBrushChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            WaveformTimeline waveformTimeline = o as WaveformTimeline;
            if (waveformTimeline != null)
                waveformTimeline.OnTimelineTickBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
        }

        /// <summary>
        /// Coerces the value of <see cref="TimelineTickBrush"/> when a new value is applied.
        /// </summary>
        /// <param name="value">The value that was set on <see cref="TimelineTickBrush"/></param>
        /// <returns>The adjusted value of <see cref="TimelineTickBrush"/></returns>
        protected virtual Brush OnCoerceTimelineTickBrush(Brush value)
        {
            return value;
        }

        /// <summary>
        /// Called after the <see cref="TimelineTickBrush"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="TimelineTickBrush"/></param>
        /// <param name="newValue">The new value of <see cref="TimelineTickBrush"/></param>
        protected virtual void OnTimelineTickBrushChanged(Brush oldValue, Brush newValue)
        {
            UpdateTimeTickInterval();
            InitTimeLabels();
        }

        /// <summary>
        /// Gets or sets a brush used to draw the tickmarks on the timeline.
        /// </summary>
        [Category("Brushes")]
        public Brush TimelineTickBrush
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (Brush)GetValue(TimelineTickBrushProperty);
            }
            set
            {
                SetValue(TimelineTickBrushProperty, value);
            }
        }
        #endregion
        
        #region ScalingCommand

        public static DependencyProperty ScalingCommandProperty
            = DependencyProperty.Register(
            "ScalingCommand",
            typeof(ICommand),
            typeof(WaveformTimeline));

        public ICommand ScalingCommand
        {
            get
            {
                return (ICommand)GetValue(ScalingCommandProperty);
            }

            set
            {
                SetValue(ScalingCommandProperty, value);
            }
        }

        #endregion

        #region Soundplayer
        public IWaveformPlayer SoundPlayer
        {
            get { return (IWaveformPlayer)GetValue(SoundPlayerProperty); }
            set { SetValue(SoundPlayerProperty, value); }
        }

        public static readonly DependencyProperty SoundPlayerProperty =
            DependencyProperty.Register("SoundPlayer", typeof(IWaveformPlayer), typeof(WaveformTimeline),
            new UIPropertyMetadata(null, new PropertyChangedCallback(OnSoundPlayerChanged)));

        private static void OnSoundPlayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            WaveformTimeline sender = d as WaveformTimeline;
            if (sender != null)
            {
                if (sender.soundPlayer != null)
                {
                    sender.soundPlayer.PropertyChanged -= sender.soundPlayer_PropertyChanged;
                }

                sender.soundPlayer = e.NewValue as IWaveformPlayer;

                if (sender.soundPlayer != null)
                {
                    sender.soundPlayer.PropertyChanged += sender.soundPlayer_PropertyChanged;
                }
            }
        }
        #endregion

        #endregion

        #region Template Overrides
        /// <summary>
        /// When overridden in a derived class, is invoked whenever application code
        /// or internal processes call System.Windows.FrameworkElement.ApplyTemplate().
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            
            waveformCanvas = GetTemplateChild("PART_Waveform") as Canvas;
            
            // Used to make the transparent regions clickable.
            waveformCanvas.Background = new SolidColorBrush(Color.FromArgb(8, 255, 255, 255));

            waveformCanvas.Children.Add(centerLine);
            waveformCanvas.Children.Add(leftPath);
            waveformCanvas.Children.Add(rightPath);

            timelineCanvas = GetTemplateChild("PART_Timeline") as RulerTimelineElement;
            timelineCanvas.LayoutBrush = TimelineTickBrush;

            repeatCanvas = GetTemplateChild("PART_Repeat") as Canvas;
            repeatCanvas.Children.Add(repeatRegion);

            progressCanvas = GetTemplateChild("PART_Progress") as Canvas;
            progressCanvas.Children.Add(progressIndicator);
            progressCanvas.Children.Add(progressLine);
        }
        
        /// <summary>
        /// Called whenever the control's template changes. 
        /// </summary>
        /// <param name="oldTemplate">The old template</param>
        /// <param name="newTemplate">The new template</param>
        protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
        {
            base.OnTemplateChanged(oldTemplate, newTemplate);
            if(waveformCanvas != null)
                waveformCanvas.Children.Clear();
            if(repeatCanvas != null)
                repeatCanvas.Children.Clear();
            if(progressCanvas != null)
                progressCanvas.Children.Clear();
        }
        #endregion

        #region Constructor
        static WaveformTimeline()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WaveformTimeline), new FrameworkPropertyMetadata(typeof(WaveformTimeline)));
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Register a sound player from which the waveform timeline
        /// can get the necessary playback data.
        /// </summary>
        /// <param name="soundPlayer">A sound player that provides waveform data through the IWaveformPlayer interface methods.</param>
        public void RegisterSoundPlayer(IWaveformPlayer soundPlayer)
        {
            this.soundPlayer = soundPlayer;
            soundPlayer.PropertyChanged += soundPlayer_PropertyChanged;
        }
        #endregion

        #region Event Handlers
        private void soundPlayer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "SelectionBegin":
                    startLoopRegion = soundPlayer.SelectionBegin.TotalSeconds;
                    UpdateRepeatRegion();
                    break;
                case "SelectionEnd":
                    endLoopRegion = soundPlayer.SelectionEnd.TotalSeconds;
                    UpdateRepeatRegion();
                    break;
                case "WaveformData":
                    GenerateWaveform();
                    break;
                case "ChannelPosition":
                    UpdateProgressIndicator();
                    break;
                case "ChannelLength":
                    startLoopRegion = -1;
                    endLoopRegion = -1;
                    UpdateAllRegions();
                    break;
            }
        }
       
        #endregion

        #region Event Overrides
        /// <summary>
        /// Raises the SizeChanged event, using the specified information as part of the eventual event data. 
        /// </summary>
        /// <param name="sizeInfo">Details of the old and new size involved in the change.</param>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            
            if (leftPath.Data == null || rightPath.Data == null) { GenerateWaveform(); }
            if (_initialSize.Width <= 1 || _initialSize.Height <= 1) _initialSize = waveformCanvas.RenderSize;

            double xScale = waveformCanvas.RenderSize.Width / _initialSize.Width;
            double yScale = waveformCanvas.RenderSize.Height / _initialSize.Height * VerticalScale;
            SetTransform(xScale, yScale);
            
            UpdateAllRegions();
        }
        
        /// <summary>
        /// Invoked when an unhandled MouseLeftButtonDown routed event is raised on this element. Implement this method to add class handling for this event.
        /// </summary>
        /// <param name="e">The MouseButtonEventArgs that contains the event data. The event data reports that the left mouse button was pressed.</param>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            
            mouseDownPoint = e.GetPosition(waveformCanvas);
            CaptureMouse();

            if (mouseDownPoint.Y >= 0)
            {
                isMouseDown = true;
            }
            else
            {
                isMouseScaling = true;
                mouseScalingPoint = mouseDownPoint;
            }
        }

        /// <summary>
        /// Invoked when an unhandled MouseLeftButtonUp routed event reaches an element in its route that is derived from this class. Implement this method to add class handling for this event.
        /// </summary>
        /// <param name="e">The MouseButtonEventArgs that contains the event data. The event data reports that the left mouse button was released.</param>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            ReleaseMouseCapture();
            if (!isMouseDown && !isMouseScaling)
                return;

            if (isMouseScaling)
            {
                isMouseScaling = false;
                return;
            }

            bool updateRepeatRegion = false;
            isMouseDown = false;
            if (Math.Abs(currentPoint.X - mouseDownPoint.X) < mouseMoveTolerance)
            {
                if (PointInRepeatRegion(mouseDownPoint))
                {
                    double position = (currentPoint.X / RenderSize.Width) * soundPlayer.ChannelLength;
                    soundPlayer.InitialChannelPosition = soundPlayer.ChannelPosition = Math.Min(soundPlayer.ChannelLength, Math.Max(0, position));
                }
                else
                {
                    soundPlayer.SelectionBegin = TimeSpan.Zero;
                    soundPlayer.SelectionEnd = TimeSpan.Zero;
                    double position = (currentPoint.X / RenderSize.Width) * soundPlayer.ChannelLength;
                    soundPlayer.InitialChannelPosition =  soundPlayer.ChannelPosition = Math.Min(soundPlayer.ChannelLength, Math.Max(0, position));
                    startLoopRegion = -1;
                    endLoopRegion = -1;
                    updateRepeatRegion = true;
                }
            }
            else
            {
                soundPlayer.SelectionBegin = TimeSpan.FromSeconds(startLoopRegion);
                soundPlayer.SelectionEnd = TimeSpan.FromSeconds(endLoopRegion);
                double position = startLoopRegion;
                soundPlayer.InitialChannelPosition = soundPlayer.ChannelPosition = Math.Min(soundPlayer.ChannelLength, Math.Max(0, position));
                updateRepeatRegion = true;
            }

            if (updateRepeatRegion)
                UpdateRepeatRegion();
        }

        /// <summary>
        /// Invoked when an unhandled Mouse.MouseMove attached event reaches an element in its route that is derived from this class. Implement this method to add class handling for this event.
        /// </summary>
        /// <param name="e">The MouseEventArgs that contains the event data.</param>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            currentPoint = e.GetPosition(waveformCanvas);

            if (isMouseScaling)
            {
                double offset = currentPoint.Y - mouseScalingPoint.Y;
                if (ScalingCommand != null && ScalingCommand.CanExecute(offset))
                    ScalingCommand.Execute(offset);

                mouseScalingPoint = currentPoint;
            }

            if (isMouseDown && AllowRepeatRegions)
            {
                if (Math.Abs(currentPoint.X - mouseDownPoint.X) > mouseMoveTolerance)
                {
                    if (mouseDownPoint.X < currentPoint.X)
                    {
                        startLoopRegion = (mouseDownPoint.X / RenderSize.Width) * soundPlayer.ChannelLength;
                        endLoopRegion = (currentPoint.X / RenderSize.Width) * soundPlayer.ChannelLength;
                    }
                    else
                    {
                        startLoopRegion = (currentPoint.X / RenderSize.Width) * soundPlayer.ChannelLength;
                        endLoopRegion = (mouseDownPoint.X / RenderSize.Width) * soundPlayer.ChannelLength;
                    }
                }
                else
                {
                    startLoopRegion = -1;
                    endLoopRegion = -1;
                }
                UpdateRepeatRegion();
            }
        }        
        #endregion

        #region Private Utiltiy Methods
        
        private bool PointInRepeatRegion(Point point)
        {
            if (soundPlayer.ChannelLength == 0)
                return false;

            double regionLeft = (soundPlayer.SelectionBegin.TotalSeconds / soundPlayer.ChannelLength) * RenderSize.Width;
            double regionRight = (soundPlayer.SelectionEnd.TotalSeconds / soundPlayer.ChannelLength) * RenderSize.Width;

            return (point.X >= regionLeft && point.X < regionRight);
        }

        private double GetTotalTransformScale()
        {
            double totalTransform = 1.0d;
            DependencyObject currentVisualTreeElement = this;
            do
            {
                Visual visual = currentVisualTreeElement as Visual;
                if (visual != null)
                {
                    Transform transform = VisualTreeHelper.GetTransform(visual);

                    // This condition is a way of determining if it
                    // was a uniform scale transform. Is there some better way?
                    if ((transform != null) &&
                        (transform.Value.M12 == 0) &&
                        (transform.Value.M21 == 0) &&
                        (transform.Value.OffsetX == 0) &&
                        (transform.Value.OffsetY == 0) &&
                        (transform.Value.M11 == transform.Value.M22))
                    {
                        totalTransform *= transform.Value.M11;
                    }
                }
                currentVisualTreeElement = VisualTreeHelper.GetParent(currentVisualTreeElement);
            }
            while (currentVisualTreeElement != null);

            return totalTransform;
        }

        private void UpdateAllRegions()
        {
            UpdateRepeatRegion();
            CreateProgressIndicator();
            UpdateTimeTickInterval();
        }

        private void UpdateRepeatRegion()
        {
            if (soundPlayer == null || repeatCanvas == null)
                return;

            double startPercent = startLoopRegion / soundPlayer.ChannelLength;
            double startXLocation = startPercent * repeatCanvas.RenderSize.Width;
            double endPercent = endLoopRegion / soundPlayer.ChannelLength;
            double endXLocation = endPercent * repeatCanvas.RenderSize.Width;

            if (soundPlayer.ChannelLength == 0 || 
                endXLocation <= startXLocation)
            {
                repeatRegion.Width = 0;
                repeatRegion.Height = 0;
                return;
            }
            
            repeatRegion.Margin = new Thickness(startXLocation, 0, 0, 0);
            repeatRegion.Width = endXLocation - startXLocation;
            repeatRegion.Height = repeatCanvas.RenderSize.Height;
        }

        double minorTickDuration = 0.05d; // Major tick = 5 seconds, Minor tick = 1.00 second

        private void UpdateTimeTickInterval()
        {
            if (soundPlayer == null || timelineCanvas == null)
                return;

            double controlWidth = this.RenderSize.Width;

            if (controlWidth / soundPlayer.ChannelLength >= 600.0d) // Major tick = 250 ms, Minor tick = 50 ms.
            {
                minorTickDuration = 0.05d;
            }
            else if (controlWidth / soundPlayer.ChannelLength >= 120.0d) // Major tick = 0.5 sec, Minor tick = 100 ms.
            {
                minorTickDuration = 0.1d;
            }
            else if (controlWidth / soundPlayer.ChannelLength >= 80.0d) // Major tick = 0.5 sec, Minor tick = 100 ms.
            {
                minorTickDuration = 0.2d;
            }
            else if (controlWidth / soundPlayer.ChannelLength >= 40.0d) // Major tick = 1 seconds, Minor tick = 200 seconds.
            {
                minorTickDuration = 0.4d;
            }
            else if (controlWidth / soundPlayer.ChannelLength >= 20.0d) // Major tick = 5 seconds, Minor tick = 1.0 seconds.
            {
                minorTickDuration = 0.8d;
            }
            else if (controlWidth / soundPlayer.ChannelLength >= 0d) // Major tick = 10 seconds, Minor tick = 2.0 seconds.
            {
                minorTickDuration = 2.0d;
            }
            timelineCanvas.TickDuration = minorTickDuration;
        }
        
        private void InitTimeLabels()
        {
            if (soundPlayer != null && timelineCanvas != null)
            {
                if (soundPlayer.ChannelLength <= minorTickDuration) return;
                timelineCanvas.Initialize(soundPlayer.ChannelLength, minorTickDuration);
                ChannelLength = SoundPlayer.ChannelLength;
            }
        }
        
        private void CreateProgressIndicator()
        {
            if (soundPlayer == null || timelineCanvas == null || progressCanvas == null)
                return;

            const double xLocation = 0.0d;

            progressLine.X1 = xLocation;
            progressLine.X2 = xLocation;
            progressLine.Y1 = timelineCanvas.RenderSize.Height;
            progressLine.Y2 = progressCanvas.RenderSize.Height;

            PolyLineSegment indicatorPolySegment = new PolyLineSegment();
            indicatorPolySegment.Points.Add(new Point(xLocation, timelineCanvas.RenderSize.Height));
            indicatorPolySegment.Points.Add(new Point(xLocation - indicatorTriangleWidth, timelineCanvas.RenderSize.Height - indicatorTriangleWidth));
            indicatorPolySegment.Points.Add(new Point(xLocation + indicatorTriangleWidth, timelineCanvas.RenderSize.Height - indicatorTriangleWidth));
            indicatorPolySegment.Points.Add(new Point(xLocation, timelineCanvas.RenderSize.Height));
            PathGeometry indicatorGeometry = new PathGeometry();
            PathFigure indicatorFigure = new PathFigure();
            indicatorFigure.Segments.Add(indicatorPolySegment);
            indicatorGeometry.Figures.Add(indicatorFigure);

            progressIndicator.Data = indicatorGeometry;
            UpdateProgressIndicator();
        }

        private void UpdateProgressIndicator()
        {
            if (soundPlayer == null || progressCanvas == null)
                return;

            double xLocation = 0.0d;
            if (soundPlayer.ChannelLength != 0)
            {
                double progressPercent = soundPlayer.ChannelPosition / soundPlayer.ChannelLength;
                xLocation = progressPercent * progressCanvas.RenderSize.Width;
            }
            progressLine.Margin = new Thickness(xLocation, 0, 0, 0);
            progressIndicator.Margin = new Thickness(xLocation, 0, 0, 0);
        }

        private void SetTransform(double xScale, double yScale)
        {
            var transform = new TransformGroup();
            var scale = new ScaleTransform(xScale, yScale);
            var translate = new TranslateTransform(0, waveformCanvas.ActualHeight / 2);

            transform.Children.Add(scale);
            transform.Children.Add(translate);

            leftPath.RenderTransform = transform;
            rightPath.RenderTransform = transform;
            centerLine.RenderTransform = transform;
        }

        private void GenerateWaveform()
        {
            const double minValue = 0;
            const double maxValue = 1.5;
            const double dbScale = (maxValue - minValue);

            rightPath.RenderTransformOrigin = new Point(0, 0);
            centerLine.RenderTransformOrigin = new Point(0, 0);
            leftPath.RenderTransformOrigin = new Point(0, 0);

            if (soundPlayer == null || soundPlayer.WaveformData == null || waveformCanvas == null ||
                waveformCanvas.RenderSize.Width < 1 || waveformCanvas.RenderSize.Height < 1)
                return;

            double leftRenderHeight;
            double rightRenderHeight;

            int pointCount = (int)(soundPlayer.WaveformData.Length / 2.0d);
            double pointThickness = waveformCanvas.RenderSize.Width / pointCount;
            double waveformSideHeight = waveformCanvas.RenderSize.Height / 2.0d * VerticalScale;

            if (CenterLineBrush != null)
            {
                centerLine.X1 = 0;
                centerLine.X2 = waveformCanvas.RenderSize.Width;
                centerLine.Y1 = 0;
                centerLine.Y2 = 0;
            }

            var WaveformLeft = new StreamGeometry();
            WaveformLeft.FillRule = FillRule.EvenOdd;
            var WaveformRight = new StreamGeometry();
            WaveformRight.FillRule = FillRule.EvenOdd;


            using (StreamGeometryContext left = WaveformLeft.Open())
            {
                using (StreamGeometryContext right = WaveformRight.Open())
                {
                    if (soundPlayer.WaveformData != null && soundPlayer.WaveformData.Length > 1)
                    {
                        left.BeginFigure(new Point(0, 0), true, true);
                        right.BeginFigure(new Point(0, 0), true, true);

                        double xLocation = 0.0d;
                        for (int i = 0; i < soundPlayer.WaveformData.Length; i += 2)
                        {
                            xLocation = (i / 2) * pointThickness;
                            leftRenderHeight = ((soundPlayer.WaveformData[i] - minValue) / dbScale) * waveformSideHeight;
                            left.LineTo(new Point(xLocation, -leftRenderHeight), true, false);
                            rightRenderHeight = ((soundPlayer.WaveformData[i + 1] - minValue) / dbScale) * waveformSideHeight;
                            right.LineTo(new Point(xLocation, rightRenderHeight), true, false);
                        }

                        left.LineTo(new Point(xLocation, 0), true, false);
                        right.LineTo(new Point(xLocation, 0), true, false);
                    }
                    else
                    {
                        WaveformLeft = null;
                        WaveformRight = null;
                    }
                }
            }
            WaveformLeft.Freeze();
            WaveformRight.Freeze();

            leftPath.Data = WaveformLeft;
            rightPath.Data = WaveformRight;

            SetTransform(1, VerticalScale);
            _initialSize = waveformCanvas.RenderSize;

            if (ChannelLength != SoundPlayer.ChannelLength)
            {
                InitTimeLabels();
            }
        }
        #endregion
    }
}
