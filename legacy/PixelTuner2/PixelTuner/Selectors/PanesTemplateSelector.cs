using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using Workspace.Layout;
using PixelTuner.ViewModel;

namespace PixelTuner.Selectors
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

        public DataTemplate ProjectPlayerViewTemplate
        {
            get;
            set;
        }

        public DataTemplate ImagesViewTemplate
        {
            get;
            set;
        }

        public DataTemplate GradientsViewTemplate
        {
            get;
            set;
        }

        public DataTemplate EffectsViewTemplate
        {
            get;
            set;
        }

        public DataTemplate VideosViewTemplate
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

            if (item is ProjectPlayerViewModel)
                return ProjectPlayerViewTemplate;

            if (item is DevicesViewModel)
                return DevicesViewTemplate;

            if (item is ProjectsViewModel)
                return ProjectsViewTemplate;

            if (item is ImagesViewModel)
                return ImagesViewTemplate;

            if (item is GradientsViewModel)
                return GradientsViewTemplate;

            if (item is EffectsViewModel)
                return EffectsViewTemplate;

            if (item is VideosViewModel)
                return VideosViewTemplate;

            return base.SelectTemplate(item, container);
        }
    }
}
