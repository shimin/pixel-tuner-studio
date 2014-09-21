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
            _pixelShader = new PixelShader() { UriSource = MakePackUri("ProjectPlayer/PixelTwister/Round.fxo") };
            this.PixelShader = _pixelShader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(TilingXProperty);
            UpdateShaderValue(DotProperty);
            UpdateShaderValue(DashProperty);
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

        #region TilingX dependency property
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
