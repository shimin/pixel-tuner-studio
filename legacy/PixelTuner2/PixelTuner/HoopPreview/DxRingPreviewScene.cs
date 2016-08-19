using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media.Animation;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.WPF;
using SharpDX.WPF.Cameras;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Resource = SharpDX.Direct3D11.Resource;

namespace DXRingPreview
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CoordVertex
    {
        public CoordVertex(Vector3 p, Vector2 t)
        {
            Pos = p;
            Tex = t;
        }
        public Vector3 Pos;
        public Vector2 Tex;

        public const int SizeInBytes = (3 + 2) * 4;
    }

    public class DxRingPreviewScene : SceneBase<D3D11>
    {
        /// <summary>
        /// Радиус обруча от 0 до 1.0
        /// </summary>
        public float CircleRadius
        {
            get { return _circleRadius; }
            set { _circleRadius = value; }
        }

        /// <summary>
        /// Ширина обруча 
        /// </summary>
        public float CircleWidth
        {
            get { return _circleWidth; }
            set { _circleWidth = value; }
        }

        /// <summary>
        /// Радиус оборота обруча вокруг центральной точки (от 0.0 до 1.0)
        /// </summary>
        public float RotationRadius
        {
            get { return _rotationRadius; }
            set { _rotationRadius = value; }
        }

        /// <summary>
        /// Управление длиной шлейфа
        /// </summary>
        public float GAlphaStep
        {
            get { return _gAlphaStep; }
            set { _gAlphaStep = value; }
        }

        /// <summary>
        /// Скорость вращения обруча (задается в единицах, значение по умолчанию 0.5)
        /// </summary>
        public float CircleRotationSpeed
        {
            get { return _circleRotationSpeed; }
            set { _circleRotationSpeed = value; }
        }

        /// <summary>
        /// Скорость сканирования картинки (задается в единицах, значение по умолчанию 0.5)
        /// </summary>
        public float CircleStripScanSpeed
        {
            get { return _circleStripScanSpeed; }
            set { _circleStripScanSpeed = value; }
        }

        /// <summary>
        /// Текстура для предпросмотра
        /// </summary>
        public string ContentTexture
        {
            set
            {
                if (_device == null || !File.Exists(value)) return;
                Disposer.SafeDispose(ref _ringTexture);
                Disposer.SafeDispose(ref _ringTextureShaderResourceView);

                _ringTexture = Resource.FromFile<Texture2D>(_device, value);
                _ringTextureShaderResourceView = new ShaderResourceView(_device, _ringTexture);    
            }
        }

        private Device _device;
        private DeviceContext _context;

        private Buffer _vertices;
        private InputLayout _layout;
        private Buffer _indicies;

        private ShaderBytecode _shaderEffectBytecode;
        private Effect _shaderEffect;

        private EffectTechnique _fillShaderEffectTechnique;
        private ShaderBytecode _fillShaderPassSignature;

        private EffectTechnique _warpRenderEffectTechnique;
        private ShaderBytecode _warpRenderEffectSignature;

        private Texture2D _ringTexture;
        private ShaderResourceView _ringTextureShaderResourceView;

        private Texture2D _fillTexture;
        private RenderTargetView _fillRenderTargetView;
        private ShaderResourceView _fillShaderResourceView;

        private float _gSlice = 0.01f;
        private float _circleRadius = 0.4f;
        private float _circleWidth = 0.01f;

        private float _rotationRadius = 0.25f;
        private float _arcPosition = 0f;

        private float _gAlphaStep = 0.7f;

        private float _circleRotationSpeed = 1f;
        private float _circleStripScanSpeed = 0.5f;

        private const int Fps = 50;
        private const int FrameDuration = 1000 / Fps;
        private const int MaxFrameSkip = 10;

        private double _nextFrameTime;        

        protected override void Attach()
        {
            if (Renderer == null) return;
           _device = Renderer.Device;
           _context = Renderer.Device.ImmediateContext;

            _nextFrameTime = GetCurrentTimeTicks();

            _shaderEffectBytecode = ShaderBytecode.CompileFromFile("./HoopPreview/shader.fx", "fx_5_0");
            _shaderEffect = new Effect(_device, _shaderEffectBytecode);

            _fillShaderEffectTechnique = _shaderEffect.GetTechniqueByName("FillTexture");
            _warpRenderEffectTechnique = _shaderEffect.GetTechniqueByName("WarpRender");

            _fillShaderPassSignature = _fillShaderEffectTechnique.GetPassByIndex(0).Description.Signature;

           _layout = new InputLayout(_device, _fillShaderPassSignature, new[] { 
                new InputElement("POSITION",0,Format.R32G32B32_Float,0,0),
                new InputElement("TEXCOORD",0,Format.R32G32_Float,12,0)
            });            

            _vertices = _device.CreateBuffer(new[]
            {
                new CoordVertex(new Vector3(-1f, 1f, 0.5f), new Vector2(0f,0f)),
                new CoordVertex(new Vector3(1f, -1f, 0.5f), new Vector2(1f,1f)),
                new CoordVertex(new Vector3(-1f, -1f, 0.5f), new Vector2(0f,1f)),
                new CoordVertex(new Vector3(1f, 1f, 0.5f), new Vector2(1f,0f))
            });

            _indicies = _device.CreateBuffer(new ushort[] { 0,1,2,0,3,1 });

            _context.InputAssembler.InputLayout = _layout;
            _context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            _context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(_vertices, CoordVertex.SizeInBytes, 0));
            _context.InputAssembler.SetIndexBuffer(_indicies, Format.R16_UInt, 0);
        }

        protected override void Reset(DrawEventArgs args)
        {
            base.Reset(args);

            Disposer.SafeDispose(ref _fillShaderResourceView);
            Disposer.SafeDispose(ref _fillRenderTargetView);
            Disposer.SafeDispose(ref _fillTexture);
 
            MakeFillTextureRenderTarget();
        }

        protected override void Detach()
        {
            base.Detach();

            Disposer.SafeDispose(ref _vertices);

            Disposer.SafeDispose(ref _layout);

            Disposer.SafeDispose(ref _context);
            Disposer.SafeDispose(ref _device);

            Disposer.SafeDispose(ref _shaderEffectBytecode);
            Disposer.SafeDispose(ref _shaderEffect);

            Disposer.SafeDispose(ref _fillShaderEffectTechnique);
            Disposer.SafeDispose(ref _fillShaderPassSignature);

            Disposer.SafeDispose(ref _ringTexture);

            Disposer.SafeDispose(ref _fillRenderTargetView);
            Disposer.SafeDispose(ref _fillShaderResourceView);
            Disposer.SafeDispose(ref _fillTexture);            

            Disposer.SafeDispose(ref _warpRenderEffectTechnique);
            Disposer.SafeDispose(ref _warpRenderEffectSignature);

        }

        public override void RenderScene(DrawEventArgs args)
        {
            var ls = 0;
            while (GetCurrentTimeTicks() > _nextFrameTime && ls < MaxFrameSkip)
            {
                _arcPosition += (1f * _circleRotationSpeed);
                _gSlice += (0.004f * _circleStripScanSpeed);

                _nextFrameTime += FrameDuration;
                ls++;
            }
            
            if (_ringTexture == null) return;

            SetFillShaderParams();
            RenderFillShader();

            SetWarpShaderParams();
            RenderWarpedImage();
        }

        private double GetCurrentTimeTicks()
        {
            return (DateTime.Now - DateTime.MinValue).TotalMilliseconds;
        }

        private void SetFillShaderParams()
        {            
            _shaderEffect.GetVariableByName("g_ContentTexture").AsShaderResource().SetResource(_ringTextureShaderResourceView);
            _shaderEffect.GetVariableByName("g_Slice").AsScalar().Set(_gSlice);
        }

        private void SetWarpShaderParams()
        {
            _shaderEffect.GetVariableByName("g_FillRenderTexture").AsShaderResource().SetResource(_fillShaderResourceView);

            _shaderEffect.GetVariableByName("circleRadius").AsScalar().Set(_circleRadius);
            _shaderEffect.GetVariableByName("circleWidth").AsScalar().Set(_circleWidth);
            _shaderEffect.GetVariableByName("rotationRadius").AsScalar().Set(_rotationRadius);
            _shaderEffect.GetVariableByName("arcPosition").AsScalar().Set(_arcPosition);

            _shaderEffect.GetVariableByName("g_alphaStep").AsScalar().Set((1f - _gAlphaStep) * 0.01f);
        }

        private void RenderFillShader()
        {
            Renderer.Device.ImmediateContext.OutputMerger.SetRenderTargets(_fillRenderTargetView);
            Renderer.Device.ImmediateContext.ClearRenderTargetView(_fillRenderTargetView, new Color4(0f, 0f, 0f, 1f));


            for (int i = 0; i < _fillShaderEffectTechnique.Description.PassCount; i++)
            {
                _fillShaderEffectTechnique.GetPassByIndex(i).Apply(_context);
                _context.DrawIndexed(6, 0, 0);
            }
        }

        private void RenderWarpedImage()
        {
            Renderer.Device.ImmediateContext.OutputMerger.SetRenderTargets(Renderer.RenderTargetView);

            for (int i = 0; i < _warpRenderEffectTechnique.Description.PassCount; i++)
            {
                _warpRenderEffectTechnique.GetPassByIndex(i).Apply(_context);
                _context.DrawIndexed(6, 0, 0);
            }
        }

        private void MakeFillTextureRenderTarget()
        {
            if (_fillTexture == null)
            {
                _fillTexture = _device.CreateTexture2D(
                    Renderer.RenderTarget.Description.Width, Renderer.RenderTarget.Description.Height,
                    BindFlags.RenderTarget | BindFlags.ShaderResource,
                    Format.R32G32B32A32_Float
                    );

                _fillRenderTargetView = new RenderTargetView(_device, _fillTexture);
                _fillShaderResourceView = new ShaderResourceView(_device, _fillTexture);
            }
        }
    }
}
