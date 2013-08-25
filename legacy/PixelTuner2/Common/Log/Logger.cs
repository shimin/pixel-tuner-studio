using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Common.Log
{
    public class Logger
    {
        private object syncLog = new object();

        private const int SpacePerTab = 8;

        private TargetBase _logTarget = null;

        public Logger(TargetBase logTarget)
        {
            _logTarget = logTarget;
        }

        /// <summary>
        /// Метод записи сообщения в протокол
        /// </summary>
        /// <param name="message">Сообщение</param>
        /// <param name="messageType">Тип сообщения</param>
        /// <param name="depth">Глубина</param>
        public void NewMessage(string message, MessageType messageType, int depth)
        {
            NewMessage(message, messageType, null, depth);
        }

        public void NewMessage(string message, MessageType messageType, string component, int depth)
        {
            lock (syncLog)
            {
                try
                {
                    if (_logTarget != null) _logTarget.NewMessage(message, messageType, component, depth);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Метод записи исключения в протокол
        /// </summary>
        /// <param name="ex">Исключение</param>
        public void WriteException(Exception ex)
        {
            if (ex == null) throw new ArgumentNullException();
            InnerWriteException(ex, 0);
        }

        public void WriteStackTrace(MessageType messageType, int depth)
        {
            StackTrace trace = new StackTrace();
            StackFrame[] frames = trace.GetFrames();
            for (int i = 1; i < frames.Length; i++)
                NewMessage(frames[i].GetMethod().Name, messageType, depth);
        }

        private void InnerWriteException(Exception ex, int depth)
        {
            NewMessage(
                    string.Format("ExceptionMessage: ", ex.Message),
                    MessageType.Error,
                    depth
                    );
            NewMessage(
                    string.Format("ExceptionStack: ", ex.StackTrace),
                    MessageType.Error,
                    depth
                    );
            if (ex.InnerException != null)
            {
                NewMessage("InnerException: ", MessageType.Error, depth + 1);
                InnerWriteException(ex.InnerException, depth + 1);
            }
        }
    }
}
