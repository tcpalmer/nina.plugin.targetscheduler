using System;
using System.Globalization;
using System.Windows.Data;

namespace NINA.Plugin.TargetScheduler.Controls.Converters {

    public class GradingStatusConverter : IValueConverter {

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            int p = Int32.Parse(parameter.ToString());
            return value.Equals(p);
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Convert.ToBoolean(value) ? parameter : null;
        }
    }
}