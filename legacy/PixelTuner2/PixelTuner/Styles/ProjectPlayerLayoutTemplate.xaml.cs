using Common.Imaging;
using GlHoopPreview;
using PixelTuner.ViewModel;
using SharpGL;
using SharpGL.Enumerations;
using SharpGL.SceneGraph;
using SharpGL.WPF;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PixelTuner.Styles
{
    public partial class ProjectPlayerLayoutTemplate : ResourceDictionary
    {
        private readonly HoopPreviewScene _hoopPreviewScene = new HoopPreviewScene();

        private string _currentFileName = "";

        private void OpenGlControl_OnOpenGLDraw(object sender, OpenGLEventArgs args)
        {
            if (args != null)
            {
                var gl = args.OpenGL;
                _hoopPreviewScene.RenderHoopPreview(gl);
                var control = (OpenGLControl)sender;
                var model = control.DataContext as ProjectPlayerViewModel;
                if (model.DisplayModel != null)
                {
                    _hoopPreviewScene.CircleSelfRotationSpeed = model.RotationFactor;
                    _hoopPreviewScene.CircleStripScanSpeed = model.ScanSpeed;
                    _hoopPreviewScene.GAlphaStep = model.TailAlpha;
                    if (model.DisplayModel.FullName != _currentFileName)
                    {
                        _currentFileName = model.DisplayModel.FullName;
                        _hoopPreviewScene.SetTexture(gl, _currentFileName);
                    }
                }
            }
        }

        private void OpenGlControl_OnResized(object sender, OpenGLEventArgs args)
        {
            if (args != null)
            {
                var c = sender as OpenGLControl;
                _hoopPreviewScene.Resize(args.OpenGL, (float)c.ActualWidth, (float)c.ActualHeight);
            }
        }

        private void OpenGLControl_OpenGLInitialized(object sender, OpenGLEventArgs args)
        {
            _hoopPreviewScene.Init(args.OpenGL);
            _hoopPreviewScene.CircleRotationSpeed = 3;
            _hoopPreviewScene.CircleWidth = 0.014f;
        }

        private void OpenGLControl_Loaded(object sender, RoutedEventArgs e)
        {
            var c = sender as OpenGLControl;
            c.Measure(new Size(10,10));
        }
    }
}
