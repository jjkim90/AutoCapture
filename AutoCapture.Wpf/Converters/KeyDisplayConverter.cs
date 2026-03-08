using System;
using System.Globalization;
using System.Windows.Data;

namespace AutoCapture.Wpf.Converters
{
    public class KeyDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var keyValue = value as string;
            return string.IsNullOrEmpty(keyValue) ? "없음" : keyValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var displayValue = value as string;
            return displayValue == "없음" ? string.Empty : displayValue;
        }
    }
}