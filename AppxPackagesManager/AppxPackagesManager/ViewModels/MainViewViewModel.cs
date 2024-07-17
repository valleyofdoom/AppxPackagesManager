using AppxPackagesManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;


namespace AppxPackagesManager.ViewModels {
    internal class MainViewViewModel : ViewModelBase {
        private ObservableCollection<PackagesGridItemModel> InternalPackagesGridItems { get; set; }

        public RelayCommand RefreshCommand => new RelayCommand(execute => Task.Run(PopulateInternalPackages));
        public RelayCommand UninstallSelectedCommand => new RelayCommand(execute => UninstallSelectedPackages(), canExecute => PackagesGridItems.Count(package => package.IsUninstall) > 0);
        public RelayCommand SelectAllCommand => new RelayCommand(execute => { SelectAllPackages(true); }, canExecute => PackagesGridItems.Count(package => package.IsUninstall) != PackagesGridItems.Count(package => package.CanUninstall));
        public RelayCommand SelectionClearCommand => new RelayCommand(execute => { SelectAllPackages(false); }, canExecute => PackagesGridItems.Count(package => package.IsUninstall) > 0);

        public string Title { get; set; }

        private string packagesCount;
        public string PackagesCount {
            get => packagesCount;
            set {
                packagesCount = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<PackagesGridItemModel> packagesGridItems;
        public ObservableCollection<PackagesGridItemModel> PackagesGridItems {
            get => packagesGridItems;
            set {
                packagesGridItems = value;
                OnPropertyChanged();
            }
        }

        private string searchQuery;
        public string SearchQuery {
            get => searchQuery;
            set {
                searchQuery = value;
                RefreshGridView();
            }
        }

        public ObservableCollection<string> PackageTypes { get; set; }

        private string selectedPackagesType;
        public string SelectedPackagesType {
            get => selectedPackagesType;
            set {
                selectedPackagesType = value;
                RefreshGridView();
            }
        }

        private bool isAllUsersPackages = false;
        public bool IsAllUsersPackages {
            get => isAllUsersPackages;
            set {
                isAllUsersPackages = value;
                _ = Task.Run(PopulateInternalPackages);
            }
        }


        private bool isWindowEnabled = true;
        public bool IsWindowEnabled {
            get => isWindowEnabled;
            set {
                isWindowEnabled = value;
                OnPropertyChanged();
            }
        }

        public MainViewViewModel() {
            // set window title
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Title = $"AppxPackagesManager v{version.Major}.{version.Minor}.{version.Build}";

            InitSelectedPackages();

            PopulateInternalPackages();
        }

        private void InitSelectedPackages() {
            PackageTypes = new ObservableCollection<string> {
                PackageType.AllPackages,
                PackageType.Packages,
                PackageType.NonRemovablePackages,
                PackageType.FrameworkPackages
            };

            SelectedPackagesType = PackageTypes.FirstOrDefault(packageType => packageType == PackageType.Packages);
        }

        private void RefreshGridView() {
            IsWindowEnabled = false;

            // gets called in SearchQuery setter but InternalPackagesGridItems is null initially
            if (InternalPackagesGridItems == null) {
                return;
            }

            PackagesGridItems = new ObservableCollection<PackagesGridItemModel>(InternalPackagesGridItems);

            // handle search query
            if (SearchQuery != null && searchQuery != "") {
                var searchQuery = SearchQuery.ToLower();

                PackagesGridItems = new ObservableCollection<PackagesGridItemModel>(
                    PackagesGridItems.Where(
                        package => package.PackageName.ToLower().Contains(searchQuery)
                        || package.FriendlyName.ToLower().Contains(searchQuery)
                    )
                );
            }

            if (SelectedPackagesType == PackageType.Packages) {
                PackagesGridItems = new ObservableCollection<PackagesGridItemModel>(
                    PackagesGridItems.Where(package => !bool.Parse(package.IsNonRemovable) && !bool.Parse(package.IsFramework))
                );
            } else if (SelectedPackagesType == PackageType.NonRemovablePackages) {
                PackagesGridItems = new ObservableCollection<PackagesGridItemModel>(
                    PackagesGridItems.Where(package => bool.Parse(package.IsNonRemovable))
                );
            } else if (SelectedPackagesType == PackageType.FrameworkPackages) {
                PackagesGridItems = new ObservableCollection<PackagesGridItemModel>(
                    PackagesGridItems.Where(package => bool.Parse(package.IsFramework))
                );
            }

            PackagesCount = $"Packages: {PackagesGridItems.Count}/{InternalPackagesGridItems.Count}";
            IsWindowEnabled = true;
        }

        private void PopulateInternalPackages() {
            IsWindowEnabled = false;

            // clear items for refresh
            InternalPackagesGridItems = new ObservableCollection<PackagesGridItemModel>();

            var packages = Utils.GetPackagesDatabase(IsAllUsersPackages);

            foreach (var package in packages) {
                var hasDependencies = package.Value.RequiredByPackages.Count != 0;

                InternalPackagesGridItems.Add(new PackagesGridItemModel {
                    CanUninstall = !hasDependencies && !package.Value.IsNonRemovable,
                    CheckBoxToolTip = hasDependencies || package.Value.IsNonRemovable ? "This package can't be uninstalled because it is required by other packages or because it is marked as non-removable" : "",
                    IsUninstall = false,
                    FriendlyName = package.Value.FriendlyName,
                    PackageName = package.Key,
                    RequiredByPackages = string.Join("\n", package.Value.RequiredByPackages),
                    Version = package.Value.Version,
                    IsNonRemovable = package.Value.IsNonRemovable.ToString(),
                    IsFramework = package.Value.IsFramework.ToString(),
                    InstallLocation = package.Value.InstallLocation,
                });
            }

            RefreshGridView();
            IsWindowEnabled = true;
        }

        private void SelectAllPackages(bool isSelectAll) {
            foreach (var package in PackagesGridItems) {
                package.IsUninstall = isSelectAll && package.CanUninstall;
            }
        }

        private async void UninstallSelectedPackages() {
            if (MessageBox.Show($"Are you sure you want to remove {PackagesGridItems.Count(package => package.IsUninstall)} package(s)?", "AppxPackagesManager", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                return;
            }

            IsWindowEnabled = false;

            var removalSucceeds = 0;
            var failedPackages = new List<string>();

            foreach (var package in PackagesGridItems) {
                // only the uninstallable packages should be checked but we can check if it can be uninstalled again
                if (package.IsUninstall && package.CanUninstall) {
                    if (await Utils.UninstallPackage(package.PackageName, IsAllUsersPackages) != 0) {
                        failedPackages.Add(package.PackageName);
                    } else {
                        removalSucceeds++;
                    }
                }
            }

            var msg = $"Removed {removalSucceeds} package(s)";

            if (failedPackages.Count != 0) {
                msg += $", failed to remove {failedPackages.Count} package(s):\n\n{string.Join("\n", failedPackages)}";
            }

            _ = MessageBox.Show(msg, "AppxPackagesManager", MessageBoxButton.OK, MessageBoxImage.Information);

            PopulateInternalPackages();
            IsWindowEnabled = true;
        }
    }
}
