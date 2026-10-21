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
        public abstract void Initialize(object parameter);

        #region Logging

        public Logger Logger { get; set; }

        protected void LogMessage(string message, MessageType mType)
        {
            if (Logger != null) Logger.NewMessage(message, mType);
        }

        protected void LogMessage(string message, MessageType mType, int level)
        {
            if (Logger != null)
            {
                Logger.NewMessage(message, mType, level);
            }
        }
        
        protected void LogException(Exception ex)
        {
            if (Logger != null) Logger.WriteException(ex);
        }

        #endregion
    }
}
