using System;
using SharpGL;

namespace GlHoopPreview.Helpers
{
    public class RenderTexure
    {
        public uint TextureName
        {
            get { return _glTextureArray[0]; }
        }

        private uint[] _glTextureArray = new uint[1] { 0 };

        public void Create(OpenGL gl, float screenWidth, float screenHeight)
        {
            Destroy(gl);

            gl.GenTextures(1, _glTextureArray);

            gl.BindTexture(OpenGL.GL_TEXTURE_2D, TextureName);

            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, (int)screenWidth, (int)screenHeight, 0, OpenGL.GL_RGB, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);
            
            gl.TexParameterI(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, new[] { OpenGL.GL_NEAREST });
            gl.TexParameterI(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, new[] { OpenGL.GL_NEAREST });
        }

        public void Bind(OpenGL gl)
        {
            //	Bind our texture object (make it the current texture).
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, TextureName);
        }

        public void BindToFrameBuffer(OpenGL gl, uint attachment)
        {
            gl.FramebufferTexture(OpenGL.GL_FRAMEBUFFER_EXT, attachment, TextureName, 0);            
            gl.DrawBuffers(1, new[] { attachment });          
        }

        public void Destroy(OpenGL gl)
        {
            if (_glTextureArray[0] != 0)
            {
                gl.DeleteTextures(1, _glTextureArray);
                _glTextureArray[0] = 0;
            }
        }
    }
}