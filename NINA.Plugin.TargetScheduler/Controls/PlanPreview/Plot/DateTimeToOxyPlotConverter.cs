using OxyPlot.Axes;
using System;
using System.Globalization;
using System.Windows.Data;

namespace NINA.Plugin.TargetScheduler.Controls.PlanPreview.Plot {

    /// <summary>
    /// Converts a DateTime into the OxyPlot double representation used for DateTime axis positions
    /// (e.g. the X value of a vertical LineAnnotation).  Returns double.NaN for an unset DateTime so
    /// the annotation is not drawn.
    /// </summary>
    public class DateTimeToOxyPlotConverter : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is DateTime dt && dt != DateTime.MinValue) {
                return DateTimeAxis.ToDouble(dt);
            }

            return double.NaN;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
