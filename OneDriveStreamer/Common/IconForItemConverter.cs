using Microsoft.OneDrive.Sdk;
using System;
using Windows.UI.Xaml.Data;

namespace OneDriveStreamer.Common
{
    class IconForItemConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                return "";
            }
            Item item = value as Item;
            if (item.Thumbnails == null)
            {
                return "";
            }
            if (item.Thumbnails.Count > 0)
            {
                return convertStringToImageSource(item.Thumbnails[0].Medium.Url);
            }
            return "";
        }

        public Windows.UI.Xaml.Media.Imaging.BitmapImage convertStringToImageSource(string source)
        {
            Windows.UI.Xaml.Media.Imaging.BitmapImage bimage = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
            bimage.UriSource = new Uri(source, UriKind.Absolute);
            return bimage;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
