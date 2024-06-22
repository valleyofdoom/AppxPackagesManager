using System.ComponentModel;

namespace AppxPackagesManager {
    public class GridItem : INotifyPropertyChanged {
        private bool _uninstall;

        public bool Uninstall {
            get => _uninstall;
            set {
                if (_uninstall != value) {
                    _uninstall = value;
                    OnPropertyChanged(nameof(Uninstall));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool CanUninstall { get; set; }

        public string PackageName { get; set; }
        public string PackageFullName { get; set; }
        public string RequiredFor { get; set; }
        public string NonRemovable { get; set; }
        public string Framework { get; set; }
    }
}
