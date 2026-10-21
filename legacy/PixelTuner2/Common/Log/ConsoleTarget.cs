using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common.Log
{
    public class ConsoleTarget : TargetBase
    {
        public override void NewMessage(string message, MessageType messageType, string component, int depth)
        {
            Console.WriteLine
                ("{3}\t[{0}]\t{4}\t{2}{1}"
                , DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fff")
                , message
                , new string('\t', depth)
                , _messageTypes[messageType]
                , component
                );
        }
    }
}
