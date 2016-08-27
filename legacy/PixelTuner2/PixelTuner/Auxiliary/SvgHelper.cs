using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Media.Animation;
using System.Windows.Media;

namespace PixelTuner.Auxiliary
{
    static class SvgHelper
    {

        public static List<T> Find<T>(Predicate<T> match, DrawingGroup root, bool recursive = true)
            where T : class
        {
            List<T> references = new List<T>();
            for (int i = 0; i < root.Children.Count; i++)
            {
                T obj = root.Children[i] as T;
                if (obj != null && match(obj))
                    references.Add(obj);
                else
                {
                    if (recursive)
                    {
                        var group = root.Children[i] as DrawingGroup;
                        if (null != group)
                            references.AddRange(Find<T>(match, group));
                    }
                }
            }
            return references;
        }

    }
}
