using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Common.Log
{
    public abstract class TargetBase
    {
        public abstract void NewMessage(string message, MessageType messageType, string component, int depth);

        public virtual int DefaultLogDepth { get; set; }

        protected Dictionary<MessageType, string> _messageTypes = null;

        protected AsyncOperation _operation = AsyncOperationManager.CreateOperation(null);

        public TargetBase()
        {
            _messageTypes = new Dictionary<MessageType, string>();
            _messageTypes[MessageType.Alert] = "ALR";
            _messageTypes[MessageType.Critical] = "CRT";
            _messageTypes[MessageType.Debug] = "DBG";
            _messageTypes[MessageType.Emergency] = "EMG";
            _messageTypes[MessageType.Error] = "ERR";
            _messageTypes[MessageType.Info] = "INF";
            _messageTypes[MessageType.Notice] = "NOT";
            _messageTypes[MessageType.Warning] = "WRN";
        }
    }
}
