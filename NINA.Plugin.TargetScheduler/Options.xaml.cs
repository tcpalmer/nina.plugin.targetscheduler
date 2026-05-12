using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Threading;

namespace NINA.Plugin.TargetScheduler {

    [Export(typeof(ResourceDictionary))]
    public partial class Options : ResourceDictionary {

        public Options() {
            InitializeComponent();
        }

        private void ReportingExpander_Expanded(object sender, RoutedEventArgs e) {
            if (sender is FrameworkElement element) {
                element.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => element.BringIntoView()));
            }
        }
    }
}