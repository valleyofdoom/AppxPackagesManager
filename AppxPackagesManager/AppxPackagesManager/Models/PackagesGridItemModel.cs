using AppxPackagesManager.ViewModels;
using System.Windows;

namespace AppxPackagesManager.Models {
    internal class PackagesGridItemModel : ViewModelBase {
        public bool CanUninstall { get; set; }
        public string CheckBoxToolTip { get; set; }

        private bool isUninstall;
        public bool IsUninstall {
            get => isUninstall;
            set {
                if (
                    value &&
                    !isUninstall &&
                    PackageName != null && // null when program starts
                    PackageName.ToLower().Contains("windowsstore") &&
                    MessageBox.Show($"Are you sure you want to remove Microsoft Store? It is not recommended as it can be used to install applications in the future", "AppxPackagesManager", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                    return;
                }

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
        public string InstallLocation { get; set; }
    }
}
