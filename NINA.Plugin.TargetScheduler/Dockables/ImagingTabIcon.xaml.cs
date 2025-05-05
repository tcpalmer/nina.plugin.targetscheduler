using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.Plugin.TargetScheduler.Dockables {

    [Export(typeof(ResourceDictionary))]
    public partial class ImagingTabIcon {

        public ImagingTabIcon() {
            InitializeComponent();
        }
    }
}