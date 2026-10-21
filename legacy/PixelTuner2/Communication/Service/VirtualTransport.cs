using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class VirtualTransport : ITransport
    {
        Stream _baseStream = new MemoryStream();
        public Stream BaseStream
        {
            get
            {
                return _baseStream;
            }
        }

        public string ChannelID
        {
            get
            {
                return "COM100";
            }
        }

        public bool IsOpen
        {
            get
            {
                return true;
            }
        }

        public int ReadTimeout
        {
            get
            {
                return 10000;
            }

            set
            {
            }
        }

        public int WriteTimeout
        {
            get
            {
                return 10000;
            }

            set
            {
            }
        }

        public void Close()
        {

        }

        public void DiscardInBuffer()
        {

        }

        public void DiscardOutBuffer()
        {

        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return count;
        }

        public int ReadByte()
        {
            return 0;
        }

        public void Write(byte[] buffer, int offset, int count)
        {

        }
    }
}
