using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.Plugin.TargetScheduler.Dockables {

    [Export(typeof(ResourceDictionary))]
    public partial class ImagingDockableTemplate {

        public ImagingDockableTemplate() {
            InitializeComponent();
        }
    }
}