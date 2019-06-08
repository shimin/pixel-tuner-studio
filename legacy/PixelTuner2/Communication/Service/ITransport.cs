using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public interface ITransport
    {
        string ChannelID { get; }
        bool IsOpen { get; }
        int ReadTimeout { get; set; }
        int WriteTimeout { get; set; }
        Stream BaseStream { get; }
        void Close();
        void DiscardInBuffer();
        void DiscardOutBuffer();
        int Read(byte[] buffer, int offset, int count);
        int ReadByte();
        void Write(byte[] buffer, int offset, int count);
    }
}
