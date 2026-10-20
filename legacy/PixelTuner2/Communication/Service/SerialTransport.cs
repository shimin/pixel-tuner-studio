using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class SerialTransport : ITransport
    {
        SerialPort _port = null;

        public SerialTransport (SerialPort port)
        {
            _port = port;
        }

        public string ChannelID
        {
            get
            {
                if (_port != null) return _port.PortName;
                else return "";
            }
        }

        public bool IsOpen
        {
            get
            {
                if (_port != null) return _port.IsOpen;
                else return false;
            }
        }

        public int ReadTimeout
        {
            get
            {
                if (_port != null) return _port.ReadTimeout;
                else return 0;
            }

            set
            {
                if (_port != null) _port.ReadTimeout = value;
            }
        }

        public int WriteTimeout
        {
            get
            {
                if (_port != null) return _port.WriteTimeout;
                else return 0;
            }

            set
            {
                if (_port != null) _port.WriteTimeout = value;
            }
        }

        public Stream BaseStream
        {
            get
            {
                if (_port != null) return _port.BaseStream;
                return null;
            }
        }

        public void Close()
        {
            if (_port != null) _port.Close();
        }

        public void DiscardInBuffer()
        {
            if (_port != null) _port.DiscardInBuffer();
        }

        public void DiscardOutBuffer()
        {
            if (_port != null) _port.DiscardOutBuffer();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (_port != null) return _port.Read(buffer, offset, count);
            else return 0;
        }

        public int ReadByte()
        {
            if (_port != null) return _port.ReadByte();
            return 0;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (_port != null) _port.Write(buffer, offset, count);
        }

        public void Dispose()
        {
            if (_port != null)
            {
                try
                {
                    if (_port.IsOpen)
                        try
                        {
                            _port.Close();
                        }
                        catch
                        {
                        }
                    GC.ReRegisterForFinalize(_port);
                    _port = null;
                }
                catch (Exception ex)
                {
                }
            }
        }
    }
}
