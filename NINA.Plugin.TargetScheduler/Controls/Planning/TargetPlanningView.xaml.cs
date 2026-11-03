using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NINA.Plugin.TargetScheduler.Controls.Planning {

    /// <summary>
    /// Interaction logic for TargetPlanningView.xaml
    /// </summary>
    public partial class TargetPlanningView : UserControl {

        public TargetPlanningView() {
            InitializeComponent();
        }

        // The panel's ScrollViewer only scrolls horizontally but would still swallow mouse wheel events, so
        // forward them to the parent to let the enclosing NINA page scroll vertically.
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if (e.Handled) {
                return;
            }

            e.Handled = true;
            MouseWheelEventArgs forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) {
                RoutedEvent = MouseWheelEvent,
                Source = sender
            };
            RaiseEvent(forwarded);
        }
    }
}
