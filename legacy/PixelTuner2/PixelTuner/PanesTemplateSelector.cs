using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using Workspace.Layout;

namespace PixelTuner
{
    public class PanesTemplateSelector : DataTemplateSelector
    {
        public PanesTemplateSelector()
        {
        
        }


        public DataTemplate FileViewTemplate
        {
            get;
            set;
        }

        public DataTemplate DevicesViewTemplate
        {
            get;
            set;
        }

        public DataTemplate ProjectsViewTemplate
        {
            get;
            set;
        }

        public DataTemplate ImagesViewTemplate
        {
            get;
            set;
        }

        public DataTemplate FileStatsViewTemplate
        {
            get;
            set;
        }

        public override System.Windows.DataTemplate SelectTemplate(object item, System.Windows.DependencyObject container)
        {
            var itemAsLayoutContent = item as LayoutContent;

            if (item is FileViewModel)
                return FileViewTemplate;

            //if (item is FileStatsViewModel)
            //    return FileStatsViewTemplate;

            if (item is DevicesViewModel)
                return DevicesViewTemplate;

            if (item is ProjectsViewModel)
                return ProjectsViewTemplate;

            if (item is ImagesViewModel)
                return ImagesViewTemplate;

            return base.SelectTemplate(item, container);
        }
    }
}
