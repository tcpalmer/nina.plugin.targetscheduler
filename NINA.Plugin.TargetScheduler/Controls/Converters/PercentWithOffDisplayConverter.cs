﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace NINA.Plugin.TargetScheduler.Controls.Converters {

    public class PercentWithOffDisplayConverter : IValueConverter {
        public const string OFF = "Off";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            double d = (double)value;
            return d == 0 ? OFF : $"{d}%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null) {
                return 0;
            }

            string s = (string)value;
            return s == OFF ? 0 : double.Parse(s.TrimEnd('%'));
        }
    }
}