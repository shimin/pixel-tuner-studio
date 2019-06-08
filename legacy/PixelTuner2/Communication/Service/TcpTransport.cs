using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Communication.Service
{
    public class TcpTransport : ITransport
    {
        private TcpClient _client = null;

        public TcpTransport(TcpClient client)
        {
            _client = client;
        }

        public string ChannelID
        {
            get
            {
                if (_client != null) return _client.Client.RemoteEndPoint.ToString();
                else return "";
            }
        }

        public Stream BaseStream
        {
            get
            {
                if (_client != null) return _client.GetStream();
                return null;
            }
        }

        public bool IsOpen
        {
            get
            {
                if (_client != null) return _client.Connected;
                return false;
            }
        }

        public int ReadTimeout
        {
            get
            {
                if (_client != null) return _client.ReceiveTimeout;
                return 0;
            }

            set
            {
                if (_client != null) _client.ReceiveTimeout = value;
            }
        }

        public int WriteTimeout
        {
            get
            {
                if (_client != null) return _client.SendTimeout;
                return 0;
            }

            set
            {
                if (_client != null) _client.SendTimeout = value;
            }
        }

        public void Close()
        {
            if (_client != null) _client.Close();
        }

        public void DiscardInBuffer()
        {
            NetworkStream ns = _client.GetStream();
            var buffer = new byte[4096];
            while (ns.DataAvailable)
            {
                ns.Read(buffer, 0, buffer.Length);
            }
        }

        public void DiscardOutBuffer()
        {

        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (_client != null)
            {
                NetworkStream ns = _client.GetStream();
                if (_client.ReceiveBufferSize > 0)
                {
                    return ns.Read(buffer, offset, count);
                }
            }
            return 0;
        }

        public int ReadByte()
        {

            if (_client != null)
            {
                NetworkStream ns = _client.GetStream();
                if (_client.ReceiveBufferSize > 0)
                {
                    var b = ns.ReadByte();
                    return b;
                }
            }
            return 0;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (_client != null)
            {
                NetworkStream ns = _client.GetStream();
                ns.Write(buffer, offset, count);
            }
        }
    }
}
