using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace NINA.Plugin.TargetScheduler.Controls.Reporting {

    /// <summary>
    /// Interaction logic for ReportingManagerView.xaml
    /// </summary>
    public partial class ReportingManagerView : UserControl {

        public ReportingManagerView() {
            InitializeComponent();
        }

        private void columnHeader_Click(object sender, RoutedEventArgs e) {
            var columnHeader = sender as DataGridColumnHeader;
            if (columnHeader != null) {
                string propertyName = GetPropertyName(GetColumnTitle(columnHeader));
                if (propertyName != null) {
                    ReportingManagerViewVM vm = this.DataContext as ReportingManagerViewVM;
                    SortDescription sortDescription = GetSortDescription(vm.ItemsView.SortDescriptions, propertyName);
                    vm.ItemsView.SortDescriptions.Clear();
                    vm.ItemsView.SortDescriptions.Add(sortDescription);
                }
            }
        }

        private SortDescription GetSortDescription(SortDescriptionCollection sortDescriptions, string propertyName) {
            if (propertyName == null) {
                return new SortDescription("AcquiredDate", ListSortDirection.Descending);
            }

            ListSortDirection sortDirection = ListSortDirection.Ascending;
            SortDescription current = sortDescriptions[0];
            if (current.PropertyName == propertyName) {
                sortDirection = current.Direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            }

            return new SortDescription(propertyName, sortDirection);
        }

        private string GetPropertyName(string title) {
            switch (title) {
                case "Date": return "AcquiredDate";
                case "Filter": return "FilterName";
                case "Stars": return "DetectedStars";
                case "HFR": return "HFR";
                case "FWHM": return "FWHM";
                case "Eccentricity": return "Eccentricity";
                case "Guiding RMS": return "GuidingRMS";
                case "Grading": return "GradingStatus";
                case "Reject Reason": return "RejectReason";
                default: return null;
            }
        }

        private string GetColumnTitle(DataGridColumnHeader columnHeader) {
            TextBlock textBlock = columnHeader?.Content as TextBlock;
            return (textBlock != null) ? textBlock.Text : null;
        }
    }
}