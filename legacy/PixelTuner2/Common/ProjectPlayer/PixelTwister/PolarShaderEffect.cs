using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Common.ProjectPlayer.PixelTwister
{
    public class PolarShaderEffect : ShaderEffect
    {
        private static PixelShader _pixelShader;

        public PolarShaderEffect()
        {
            try
            {
                _pixelShader = new PixelShader() { UriSource = MakePackUri("ProjectPlayer/PixelTwister/Round.fxo") };
                this.PixelShader = _pixelShader;
            }
            catch (Exception ex)
            {
            }
            
            UpdateShaderValue(InputProperty);
            UpdateShaderValue(TilingXProperty);
            UpdateShaderValue(DotProperty);
            UpdateShaderValue(DashProperty);
            UpdateShaderValue(ArcPositionProperty);
            UpdateShaderValue(ArcLengthProperty);
        }
        
        #region Input dependency property
        public Brush Input
        {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(PolarShaderEffect), 0);

        #endregion 
    
        #region ArcPosition dependency property
        public double ArcPosition
        {
            get { return (double)GetValue(ArcPositionProperty); }
            set { SetValue(ArcPositionProperty, value); }
        }

        public static readonly DependencyProperty ArcPositionProperty =
            DependencyProperty.Register("ArcPosition", typeof(double), typeof(PolarShaderEffect),
              new UIPropertyMetadata(0.4d, PixelShaderConstantCallback(3)));
        #endregion

        #region ArcLength dependency property
        public double ArcLength
        {
            get { return (double)GetValue(ArcLengthProperty); }
            set { SetValue(ArcLengthProperty, value); }
        }

        public static readonly DependencyProperty ArcLengthProperty =
            DependencyProperty.Register("ArcLength", typeof(double), typeof(PolarShaderEffect),
              new UIPropertyMetadata(0.3d, PixelShaderConstantCallback(4)));
        #endregion

        #region MinimalIntensity dependency property
        public double MinimalIntensity
        {
            get { return (double)GetValue(MinimalIntensityProperty); }
            set { SetValue(MinimalIntensityProperty, value); }
        }

        public static readonly DependencyProperty MinimalIntensityProperty =
            DependencyProperty.Register("MinimalIntensity", typeof(double), typeof(PolarShaderEffect),
              new UIPropertyMetadata(0.0d, PixelShaderConstantCallback(5)));
        #endregion


        #region PoiWidth dependency property
        public double PoiWidth
        {
            get { return (double)GetValue(PoiWidthProperty); }
            set { SetValue(PoiWidthProperty, value); }
        }

        public static readonly DependencyProperty PoiWidthProperty =
            DependencyProperty.Register("PoiWidth", typeof(double), typeof(PolarShaderEffect),
              new UIPropertyMetadata(0.05d, PixelShaderConstantCallback(6)));
        #endregion

        #region ClampValue dependency property
        public double ClampValue
        {
            get { return (double)GetValue(ClampValueProperty); }
            set { SetValue(ClampValueProperty, value); }
        }

        public static readonly DependencyProperty ClampValueProperty =
            DependencyProperty.Register("ClampValue", typeof(double), typeof(PolarShaderEffect),
              new UIPropertyMetadata(1.0d, PixelShaderConstantCallback(7)));
        #endregion

        #region Opacity dependency property
        public double TilingX
        {
            get { return (double)GetValue(TilingXProperty); }
            set { SetValue(TilingXProperty, value); }
        }

        public static readonly DependencyProperty TilingXProperty =
            DependencyProperty.Register("TilingX", typeof(double), typeof(PolarShaderEffect),
              new UIPropertyMetadata(1.0d, PixelShaderConstantCallback(0)));
        #endregion

        #region Dot dependency property
        public double Dot
        {
            get { return (double)GetValue(DotProperty); }
            set { SetValue(DotProperty, value); }
        }

        public static readonly DependencyProperty DotProperty =
            DependencyProperty.Register("Dot", typeof(double), typeof(PolarShaderEffect),
              new UIPropertyMetadata(0.012d, PixelShaderConstantCallback(1)));
        #endregion

        #region Dash dependency property
        public double Dash
        {
            get { return (double)GetValue(DashProperty); }
            set { SetValue(DashProperty, value); }
        }

        public static readonly DependencyProperty DashProperty =
            DependencyProperty.Register("Dash", typeof(double), typeof(PolarShaderEffect),
              new UIPropertyMetadata(0.005d, PixelShaderConstantCallback(2)));
        #endregion

        public static Uri MakePackUri(string relativeFile)
        {
            Assembly a = typeof(Common.ProjectPlayer.PixelTwister.PolarShaderEffect).Assembly;

            // Extract the short name.
            string assemblyShortName = a.ToString().Split(',')[0];

            string uriString = "pack://application:,,,/" +
                assemblyShortName +
                ";component/" +
                relativeFile;

            return new Uri(uriString);
        }
    }
}
