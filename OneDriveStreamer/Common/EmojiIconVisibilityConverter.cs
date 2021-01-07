using Microsoft.OneDrive.Sdk;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace OneDriveStreamer.Common
{
    class EmojiIconVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                return Visibility.Visible;
            }

            Item item = value as Item;
            if (item.Thumbnails.Count > 0)
            {
                return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
