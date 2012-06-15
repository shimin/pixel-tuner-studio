using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeLineTool
{
    public interface ITimeLineDataItemConvertable <T> : ITimeLineDataItem
    {
        T ConvertTo (ITimeLineDataItem item);
        ITimeLineDataItem ConvertFrom(T item);
    }
}
