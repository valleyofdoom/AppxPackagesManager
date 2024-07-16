using AppxPackagesManager.ViewModels;

namespace AppxPackagesManager.Models {
    internal class PackagesGridItemModel : ViewModelBase {
        public bool CanUninstall { get; set; }
        public string CheckBoxToolTip { get; set; }

        private bool isUninstall;
        public bool IsUninstall {
            get => isUninstall;
            set {
                isUninstall = value;
                OnPropertyChanged();
            }
        }

        public string FriendlyName { get; set; }
        public string PackageName { get; set; }
        public string RequiredByPackages { get; set; }
        public string Version { get; set; }
        public string IsNonRemovable { get; set; }
        public string IsFramework { get; set; }
    }
}
