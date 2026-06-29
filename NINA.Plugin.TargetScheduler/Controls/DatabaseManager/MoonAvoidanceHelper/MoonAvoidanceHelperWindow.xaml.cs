using System.Windows;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager.MoonAvoidanceHelper {

    public partial class MoonAvoidanceHelperWindow : Window {

        // The dialog is tall and grows downward when "Show Example" is expanded; nudge it up from the centered
        // position when it first opens so there's room to grow without running off the bottom of the screen.
        private const double InitialUpwardOffset = 150;

        public MoonAvoidanceHelperWindow() {
            InitializeComponent();

            // Adjust position in ContentRendered, not Loaded: with SizeToContent=Height + CenterOwner the window
            // is re-centered after Loaded fires (once its content-driven size is final), which would overwrite an
            // earlier Top change.  ContentRendered runs after positioning has settled, so the nudge sticks.
            ContentRendered += OnContentRendered;
        }

        private void OnContentRendered(object sender, System.EventArgs e) {
            ContentRendered -= OnContentRendered;
            if (!double.IsNaN(Top)) {
                Top = System.Math.Max(0, Top - InitialUpwardOffset);
            }
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
