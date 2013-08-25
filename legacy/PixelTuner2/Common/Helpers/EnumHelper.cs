using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Common.Helpers
{
    public class EnumHelper
    {
        public static string GetDescription(Enum value)
        {
            System.Reflection.FieldInfo fi = value.GetType().GetField(value.ToString());
            DescriptionAttribute[] attributes =
                  (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return (attributes.Length > 0) ? attributes[0].Description : value.ToString();
        }
    }
}
