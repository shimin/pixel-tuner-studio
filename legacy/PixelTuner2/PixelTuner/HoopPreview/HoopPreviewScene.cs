using System;
using System.Collections.Generic;
using System.Drawing;
using GlHoopPreview.Geometry;
using GlHoopPreview.Helpers;
using GlmNet;
using SharpGL;
using SharpGL.SceneGraph.Assets;
using SharpGL.Shaders;

namespace GlHoopPreview
{
    public static class VertexAttributes
    {
        public const uint Position = 0;
        public const uint TexCoord = 1;
    }

    public class HoopPreviewScene
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
        /// Скорость вращения обруча вокруг своей оси
        /// </summary>
        public float CircleSelfRotationSpeed
        {
            get { return _circleSelfRotationSpeed; }
            set { _circleSelfRotationSpeed = value; }
        }  

        public void Init(OpenGL gl)
        {
            InitOpenglSettings(gl);

            _sliceShader = LoadShaderProgram(gl, @"HoopPreview\Shaders\SliceShader.vert", @"HoopPreview\Shaders\SliceShader.frag");
            _warpShader = LoadShaderProgram(gl, @"HoopPreview\Shaders\WarpShader.vert", @"HoopPreview\Shaders\WarpShader.frag");
            _clearShader = LoadShaderProgram(gl, @"HoopPreview\Shaders\ClearShader.vert", @"HoopPreview\Shaders\ClearShader.frag");

            _simpleQuad.CreateGeometry(gl, VertexAttributes.Position, VertexAttributes.TexCoord);
        }

        public void RenderHoopPreview(OpenGL gl)
        {
            UpdateHoopParams();

            SetSliceShaderParams(gl);
            SetWarpShaderParams(gl);

            gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _renderTextureBufferId);
            _renderTexure.BindToFrameBuffer(gl, OpenGL.GL_COLOR_ATTACHMENT0_EXT);
            gl.Viewport(0, 0, (int)_currentScreenWidth, (int)_currentScreenHeight);

            gl.Disable(OpenGL.GL_BLEND);
            _quadTexture.Bind(gl);
            _simpleQuad.Render(gl, _sliceShader);

            gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 1);

            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendEquation(OpenGL.GL_MAX_EXT);
            gl.BlendFunc(OpenGL.GL_ONE, OpenGL.GL_ONE);

            _renderTexure.Bind(gl);

            _simpleQuad.Render(gl, _warpShader);

            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendEquation(OpenGL.GL_FUNC_REVERSE_SUBTRACT_EXT);
            gl.BlendFunc(OpenGL.GL_ONE, OpenGL.GL_ONE);
            _simpleQuad.Render(gl, _clearShader);
        }

        public void Resize(OpenGL gl, float screenWidth, float screenHeight)
        {
            _currentScreenWidth = screenWidth;
            _currentScreenHeight = screenHeight;

            var bufIds = new uint[1];
            gl.GenFramebuffersEXT(1, bufIds);
            _renderTextureBufferId = bufIds[0];

            _renderTexure.Create(gl, screenWidth, screenHeight);

            CreateOrtoMatrix(gl);
            Clear(gl);
        }

        public void InitOpenglSettings(OpenGL gl)
        {
            gl.Enable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_VERTEX_SHADER);
            gl.Enable(OpenGL.GL_FRAGMENT_SHADER);

            //_nextFrameTime = GetCurrentTimeTicks();
        }

        public void Clear(OpenGL gl)
        {
            gl.ClearColor(0f, 0f, 0f, 1f);
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT | OpenGL.GL_STENCIL_BUFFER_BIT);                       
        }

        public void SetTexture(OpenGL gl, string texturePath)
        {
            if (_quadTexture == null) return;            
            _quadTexture.Destroy(gl);
            _quadTexture.Create(gl, texturePath);            
        }

        public void SetTexture(OpenGL gl, Bitmap texture)
        {
            if (_quadTexture == null) return;
            _quadTexture.Destroy(gl);
            _quadTexture.Create(gl, texture);
        }
        
        private void CreateOrtoMatrix(OpenGL gl)
        {
            _projectionMatrix = glm.ortho(0f, _currentScreenWidth, _currentScreenHeight, 0f);
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            gl.MultMatrix(_projectionMatrix.to_array());
        }

        private void UpdateHoopParams()
        {
            _arcSelfRotation += (0.001f * _circleSelfRotationSpeed);
            _arcPosition += (1f * _circleRotationSpeed);
            _gSlice += (0.004f * _circleStripScanSpeed);
        }

        private void SetSliceShaderParams(OpenGL gl)
        {
            _sliceShader.Bind(gl);
            _sliceShader.SetUniform1(gl, "slicePosition", _gSlice);
            _sliceShader.Unbind(gl);
        }

        private void SetWarpShaderParams(OpenGL gl)
        {
            _warpShader.Bind(gl);

            _warpShader.SetUniform1(gl, "circleRadius", _circleRadius);
            _warpShader.SetUniform1(gl, "circleWidth", _circleWidth);
            _warpShader.SetUniform1(gl, "rotationRadius", _rotationRadius);
            _warpShader.SetUniform1(gl, "arcPosition", _arcPosition);
            _warpShader.SetUniform1(gl, "sliceShift", _arcSelfRotation);

            _warpShader.Unbind(gl);

            _clearShader.Bind(gl);
            _clearShader.SetUniform1(gl, "g_alphaStep", 0.1f - (_gAlphaStep * 0.1f));
            _clearShader.Unbind(gl);
        }

        private double GetCurrentTimeTicks()
        {
            return (DateTime.Now - DateTime.MinValue).TotalMilliseconds;
        }

        private ShaderProgram LoadShaderProgram(
            OpenGL gl,
            string vertexShaderPath,
            string fragmentShaderPath
        )
        {
            var attributeLocations = new Dictionary<uint, string>
            {
                {VertexAttributes.Position, "position"},
                {VertexAttributes.TexCoord, "texcoord"},
            };

            var shaderProgram = new ShaderProgram();
            shaderProgram.Create(
                gl,
                Helpers.ManifestResourceLoader.LoadTextFile(vertexShaderPath),
                Helpers.ManifestResourceLoader.LoadTextFile(fragmentShaderPath),
                attributeLocations
            );

            shaderProgram.AssertValid(gl);

            return shaderProgram;
        }

        private uint _renderTextureBufferId = 0;

        private mat4 _modelviewMatrix = mat4.identity();
        private mat4 _projectionMatrix = mat4.identity();
        //private mat3 _normalMatrix = mat3.identity();
        
        private readonly Texture _quadTexture = new Texture();
        private readonly RenderTexure _renderTexure = new RenderTexure();

        private readonly SimpleQuad _simpleQuad = new SimpleQuad();
             
        private ShaderProgram _sliceShader;
        private ShaderProgram _warpShader;
        private ShaderProgram _clearShader;

        private float _gSlice = 0.01f;
        private float _circleRadius = 0.4f;
        private float _circleWidth = 0.01f;

        private float _rotationRadius = 0.25f;
        private float _arcPosition = 0f;
        private float _arcSelfRotation = 0f;

        private float _gAlphaStep = 0.9f;

        private float _circleRotationSpeed = 1.5f;
        private float _circleStripScanSpeed = 0.5f;
        private float _circleSelfRotationSpeed = 1f;

        private float _currentScreenWidth;
        private float _currentScreenHeight;

        //private const int Fps = 50;
        //private const int FrameDuration = 1000 / Fps;
        //private const int MaxFrameSkip = 1;

        //private double _nextFrameTime;   
    }
}
