using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace OneDriveStreamer.Common
{
    class MovieDurationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int len = System.Convert.ToInt32(value);
            return ReadableTime(len);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        public static string ReadableTime(int milliseconds)
        {
            var parts = new List<string>();
            Action<int, string> add = (val, unit) => { if (val > 0) parts.Add(val + unit); };
            var t = TimeSpan.FromMilliseconds(milliseconds);

            add(t.Days, "d");
            add(t.Hours, "h");
            add(t.Minutes, "m");
            add(t.Seconds, "s");
            // add(t.Milliseconds, "ms");

            return string.Join(" ", parts);
        }
    }
}
