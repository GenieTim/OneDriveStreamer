using Microsoft.OneDrive.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Controls;

namespace OneDriveStreamer.Common
{
    class SymbolFinder : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                return "";
            }
            Item item = value as Item;
            if (item.Thumbnails == null || item.Thumbnails.Count < 1)
            {
                return this.getIcon(item);
            }
            return "";
        }

        public Symbol getIcon(Item item)
        {
            // TODO: design/find icons to return here
            if (item.Folder != null)
            {
                var symbol = Symbol.Folder;
                return symbol;
            }
            else if (item.Audio != null)
            {
                return Symbol.Audio;
            }
            else if (item.Video != null)
            {
                return Symbol.Video;
            }
            else if (item.Photo != null)
            {
                return Symbol.Pictures;
            }
            return Symbol.OpenFile;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
