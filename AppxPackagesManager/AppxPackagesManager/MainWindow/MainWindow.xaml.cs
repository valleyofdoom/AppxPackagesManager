using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

using Microsoft.Windows.Appx.PackageManager.Commands;

using Windows.Management.Deployment;

namespace AppxPackagesManager {
    public partial class MainWindow : Window {
        private readonly Dictionary<string, Dictionary<string, object>> _appxPackages = new Dictionary<string, Dictionary<string, object>>();
        private readonly ObservableCollection<GridItem> _packagesGridItems = new ObservableCollection<GridItem>();
        private readonly PackageManager _packageManager = new PackageManager();

        public MainWindow() {
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Title = $"AppxPackagesManager v{version.Major}.{version.Minor}.{version.Build}";

            GetPackageData();
            GetAppxPackages();
        }

        private void GetPackageData() {
            _appxPackages.Clear();

            using (var ps = PowerShell.Create()) {
                _ = ps.AddCommand("Get-AppxPackage");
                var results = ps.Invoke();

                foreach (var result in results) {
                    var packageFullName = result.Properties["PackageFullName"].Value.ToString();

                    foreach (var dependency in (AppxPackage[])result.Properties["Dependencies"].Value) {
                        var dependencyName = dependency.PackageFullName;

                        // dependency data might not already exist so we need to add it now
                        if (_appxPackages.ContainsKey(dependencyName)) {
                            (_appxPackages[dependencyName]["required_for"] as List<string>).Add(packageFullName);
                        } else {
                            _appxPackages[dependencyName] = new Dictionary<string, object> {
                                { "required_for", new List<string> { packageFullName } }
                            };
                        }
                    }

                    var friendlyName = result.Properties["Name"].Value.ToString();
                    var isFramework = result.Properties["IsFramework"].Value;

                    object isNonRemovable = null;
                    try {
                        isNonRemovable = result.Properties["NonRemovable"].Value;
                    } catch {
                        // ignore
                    }

                    try {
                        var doc = XDocument.Load($"{result.Properties["InstallLocation"].Value}\\AppxManifest.xml");
                        XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";

                        var displayName = doc.Element(ns + "Package").Element(ns + "Properties").Element(ns + "DisplayName").Value;

                        if (!displayName.StartsWith("ms-resource")) {
                            friendlyName = displayName;
                        }
                    } catch {
                        // ignore
                    }

                    // check if package exists in case it was added as a dependency already
                    if (_appxPackages.ContainsKey(packageFullName)) {
                        _appxPackages[packageFullName]["name"] = friendlyName;
                        _appxPackages[packageFullName]["is_framework"] = isFramework;
                        _appxPackages[packageFullName]["is_non_removable"] = isNonRemovable;
                    } else {
                        _appxPackages[packageFullName] = new Dictionary<string, object> {
                            { "name",  friendlyName},
                            { "required_for",  new List<string>() },
                            { "is_framework",  isFramework },
                            { "is_non_removable",  isNonRemovable },
                        };
                    }
                }

                if (ps.HadErrors) {
                    //foreach (var error in ps.Streams.Error) {
                    //    Console.WriteLine(error.Exception.Message);
                    //}
                }
            }

            // handle custom dependencies
            var packages = new Dictionary<string, List<string>> {
                // example placeholder
                //{ "Microsoft.Todos_1.48.21892.0_x64__8wekyb3d8bbwe" , new List<string> { "Microsoft.WindowsFileRecovery_0.1.20151.0_x64__8wekyb3d8bbwe" } }
            };

            foreach (var packageFullName in packages.Keys) {
                foreach (var dependencyName in packages[packageFullName]) {
                    if (_appxPackages.ContainsKey(packageFullName) && _appxPackages.ContainsKey(dependencyName)) {
                        (_appxPackages[dependencyName]["required_for"] as List<string>).Add(packageFullName);
                    }
                }
            }
        }

        private void GetAppxPackages() {
            // keep a note of what was checked before clearing
            var prevPackagesGridItemsChecked = new Dictionary<string, object>();

            foreach (var item in _packagesGridItems) {
                prevPackagesGridItemsChecked.Add(item.PackageFullName, item.Uninstall);
            }

            // clear existing items
            _packagesGridItems.Clear();

            foreach (var packageFullName in _appxPackages.Keys) {
                var package = _appxPackages[packageFullName];

                var isFramework = GetValue(package, "is_framework", false);
                var isNonRemovable = GetValue(package, "is_non_removable", false);


                if ((hideFrameworkPackages.IsChecked is true && isFramework is true) || (hideNonRemovablePackages.IsChecked is true && isNonRemovable is true)) {
                    continue;
                }

                var requiredFor = (List<string>)GetValue(package, "required_for", new List<string>());

                var packagesGridItem = new GridItem {
                    Uninstall = (bool)GetValue(prevPackagesGridItemsChecked, packageFullName, false),
                    CanUninstall = requiredFor.Count == 0,
                    PackageName = GetValue(package, "name", "Unknown").ToString(),
                    PackageFullName = packageFullName,
                    RequiredFor = string.Join("\n", requiredFor),
                    NonRemovable = isNonRemovable is null ? "Unknown" : (isNonRemovable is true).ToString(),
                    Framework = GetValue(package, "is_framework", "Unknown").ToString(),
                };

                _packagesGridItems.Add(packagesGridItem);
            }

            // update the source for datagrid
            packagesDataGrid.ItemsSource = _packagesGridItems;
        }

        private void CheckAllPackages(bool isChecked) {
            foreach (GridItem package in packagesDataGrid.Items) {
                if (package.CanUninstall) {
                    package.Uninstall = isChecked;
                }
            }
        }

        private object GetValue(Dictionary<string, object> dict, string key, object defaultValue) {
            return dict.ContainsKey(key) ? dict[key] : defaultValue;
        }

        private void HideFrameworkPackagesClick(object sender, RoutedEventArgs e) {
            GetAppxPackages();
        }

        private void RefreshList() {
            GetPackageData();
            GetAppxPackages();
        }

        private void RefreshListClick(object sender, RoutedEventArgs e) {
            RefreshList();
        }

        private void SelectAllClick(object sender, RoutedEventArgs e) {
            CheckAllPackages(true);
        }

        private void ClearSelectionClick(object sender, RoutedEventArgs e) {
            CheckAllPackages(false);
        }

        private void HideNonRemovablePackagesClick(object sender, RoutedEventArgs e) {
            GetAppxPackages();
        }

        private void FilterDataGrid(string query) {
            var filteredPackages = _packagesGridItems.Where(p => p.PackageName.ToLower().Contains(query.ToLower())).ToList();
            packagesDataGrid.ItemsSource = filteredPackages;
        }

        private void SearchBoxTextChanged(object sender, TextChangedEventArgs e) {
            FilterDataGrid(searchBox.Text);
        }

        private void UninstallPackagesClick(object sender, RoutedEventArgs e) {
            var totalPackages = 0;

            // uninstall selected packages
            foreach (GridItem package in packagesDataGrid.Items) {
                if (package.CanUninstall && package.Uninstall) {
                    var deploymentResult = _packageManager.RemovePackageAsync(package.PackageFullName).GetResults();

                    totalPackages++;
                }
            }

            _ = MessageBox.Show(totalPackages == 0
                ? "No packages selected"
                : "Attempted to remove selected packages.\nPress OK to refresh list", "AppxPackagesManager", MessageBoxButton.OK, MessageBoxImage.Information);

            RefreshList();
        }
    }
}
