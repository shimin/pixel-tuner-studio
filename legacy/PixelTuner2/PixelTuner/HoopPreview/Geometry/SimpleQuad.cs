using System;
using System.Linq;
using System.Windows.Controls;
using GlmNet;
using SharpGL;
using SharpGL.VertexBuffers;
using SharpGL.Shaders;

namespace GlHoopPreview.Geometry
{
    public class SimpleQuad
    {
        public void CreateGeometry(
            OpenGL gl, 
            uint vertexAttributeLocation, 
            uint texcoordAttributelocation
        )
        {
            _verticies = new []
            {
                new vec3(-1f,-1f,0f),
                new vec3(-1f, 1f, 0f),
                new vec3(1f, 1f, 0f),
                new vec3(1f, -1f, 0f) 
            };

            _texcoord = new[]
            {
                new vec2(0f,0f),
                new vec2(1f,0f),
                new vec2(1f,1f),
                new vec2(0f,1f) 
            };

            _indicies = new ushort[] { 0,1,2,0,2,3 };
            
            _vertexBufferArray = new VertexBufferArray();
            _vertexBufferArray.Create(gl);
            _vertexBufferArray.Bind(gl);

            var vertexBuffer = new VertexBuffer();
                vertexBuffer.Create(gl);
                vertexBuffer.Bind(gl);
                vertexBuffer.SetData(gl, vertexAttributeLocation, _verticies.SelectMany(vec3 => vec3.to_array()).ToArray(), false, 3);

            var texcoordBuffer = new VertexBuffer();
                texcoordBuffer.Create(gl);
                texcoordBuffer.Bind(gl);
                texcoordBuffer.SetData(gl, texcoordAttributelocation, _texcoord.SelectMany(vec2 => vec2.to_array()).ToArray(), false, 2);

            var indexBuffer = new IndexBuffer();
                indexBuffer.Create(gl);
                indexBuffer.Bind(gl);
                indexBuffer.SetData(gl, _indicies);

            _vertexBufferArray.Unbind(gl);
        }

        public void Render(OpenGL gl, ShaderProgram shader)
        {
            _vertexBufferArray.Bind(gl);
                        
            shader.Bind(gl);           
            gl.DrawElements(OpenGL.GL_TRIANGLES, _indicies.Length, OpenGL.GL_UNSIGNED_SHORT, IntPtr.Zero);
            shader.Unbind(gl);
        }

        private vec3[] _verticies;
        private vec2[] _texcoord;
        private ushort[] _indicies;

        private VertexBufferArray _vertexBufferArray;

        public VertexBufferArray BufferArray
        {
            get { return _vertexBufferArray; }
        }
    }    
}