using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;

namespace FlexButton
{
    public class FontSizeToViewBoxDirectionConverter : IValueConverter
    {
        public static readonly FontSizeToViewBoxDirectionConverter Instance = new FontSizeToViewBoxDirectionConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (double)value > 15 ? StretchDirection.DownOnly : StretchDirection.Both;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
