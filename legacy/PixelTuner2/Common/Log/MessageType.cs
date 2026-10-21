using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common.Log
{
    public enum MessageType
    {
        Emergency = 0,
        Alert = 1,
        Critical = 2,
        Error = 3,
        Warning = 4,
        Notice = 5,
        Info = 6,
        Debug = 7,
    }
}
