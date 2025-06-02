using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NINA.Plugin.TargetScheduler.Controls.Converters {

    public class StatusConverter : IValueConverter {
        private static object lockObj = new object();
        private static bool init = false;
        private static GeometryGroup CheckMarkSVG;
        private static GeometryGroup XMarkSVG;

        public StatusConverter() {
            initIcons();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            bool status = (bool)value;
            return status ? CheckMarkSVG : XMarkSVG;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }

        private void initIcons() {
            lock (lockObj) {
                if (!init) {
                    var resourceDict = new ResourceDictionary();
                    resourceDict.Source = new Uri("NINA.Plugin.TargetScheduler;component/Controls/Resources/SVGDictionary.xaml", UriKind.RelativeOrAbsolute);
                    CheckMarkSVG = (GeometryGroup)resourceDict["SS_CheckMarkSVG"]; CheckMarkSVG.Freeze();
                    XMarkSVG = (GeometryGroup)resourceDict["SS_CheckMarkSVG"]; XMarkSVG.Freeze();
                    init = true;
                }
            }
        }
    }

    public class StatusMarkColorConverter : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            bool status = (bool)value;
            return status ? "Green" : "Crimson";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}