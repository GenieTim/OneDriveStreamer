using System;
using Windows.UI.Xaml.Data;

namespace OneDriveStreamer.Common
{
    class IntegerFileSizeStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            if (value == null)
            {
                return "";
            }
            double len = double.Parse(value.ToString());
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            string result = String.Format("{0:0.##} {1}", len, sizes[order]);
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
