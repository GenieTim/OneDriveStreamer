using Microsoft.OneDrive.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace OneDriveStreamer.Common
{
    class OneDriveItemDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FolderTemplate { get; set; }
        public DataTemplate MovieTemplate { get; set; }
        public DataTemplate ImageTemplate { get; set; }
        public DataTemplate DefaultTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            var sdkItem = item as Item;
            if (sdkItem != null)
            {
                if (sdkItem.Folder != null)
                {
                    return FolderTemplate;
                }
                if (sdkItem.Video != null)
                {
                    return MovieTemplate;
                }
                if (sdkItem.Image != null)
                {
                    return ImageTemplate;
                }
            }
            // default
            return DefaultTemplate;
            // return base.SelectTemplateCore(item);
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }
    }
}
