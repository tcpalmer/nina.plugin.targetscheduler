using System.Windows;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public partial class MoonAvoidanceHelperWindow : Window {

        public MoonAvoidanceHelperWindow() {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
