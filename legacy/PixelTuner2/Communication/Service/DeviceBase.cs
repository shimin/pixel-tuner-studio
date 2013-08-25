using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Log;

namespace Communication.Service
{
    public delegate void InitProcessStatus(int min, int max, int start = 0, int step = 1);
    public delegate void PerformStep(ConnectionStatus status);
    public delegate void SetStatus(ConnectionStatus status);

    public abstract class DeviceBase
    {
        protected virtual void Initialize(object parameter) { }

        #region Logging

        public Logger Logger { get; set; }

        protected void LogMessage(string message, MessageType mType, int level)
        {
            if (Logger != null)
            {
                Logger.NewMessage(message, mType, level);
            }
        }

        protected string BytesToString(byte[] buf)
        {
            string result = "";
            foreach (byte b in buf) result += b.ToString("X2") + " ";
            return result;
        }

        protected string BytesToString(List<byte> buf)
        {
            return BytesToString(buf.ToArray());
        } 

        #endregion
    }
}
