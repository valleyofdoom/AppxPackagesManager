using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace AppxPackagesManager {
    internal class Utils {
        private static readonly PackageManager packageManager = new PackageManager();
        private static readonly XNamespace xdocNamespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";

        private static bool IsPackageFamilyInUninstallBlocklist(string packageFamilyName) {
            var isPresent = false;

            if (NativeMethods.IsPackageFamilyInUninstallBlocklist(packageFamilyName, ref isPresent) < 0) {
                _ = MessageBox.Show($"Failed to determine whether package family {packageFamilyName} is in uninstall blocklist", "AppxPackagesManager", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            return isPresent;
        }

        private static string GetManifestFriendlyName(string manifestLocation) {
            var xdoc = XDocument.Load(manifestLocation);

            var displayName = xdoc.Element(xdocNamespace + "Package").Element(xdocNamespace + "Properties").Element(xdocNamespace + "DisplayName").Value;

            return displayName;
        }

        private static string GetPackageFriendlyName(Package package) {
            var friendlyName = "";

            try {
                friendlyName = package.DisplayName;
            } catch {
                // ignore as it will be fetched by other means
            }

            if (friendlyName == "") {
                // attempt to get the friendly name from the manifest
                var manifestLocation = $"{package.InstalledLocation.Path}\\AppxManifest.xml";

                if (File.Exists(manifestLocation)) {
                    try {
                        var manifestDisplayName = GetManifestFriendlyName(manifestLocation);

                        // ignore invalid display names
                        if (!manifestDisplayName.StartsWith("ms-resource")) {
                            friendlyName = manifestDisplayName;
                        }
                    } catch (NullReferenceException) {
                        // ignore
                    }
                }
            }

            if (friendlyName == "") {
                friendlyName = package.Id.Name;
            }

            return friendlyName;
        }

        private static void AddPackageToDatabase(Dictionary<string, PackageInfo> database, string packageFullName, Package package) {
            // add package to database
            var packageVersion = package.Id.Version;

            var installLocation = "";

            try {
                installLocation = package.InstalledLocation.Path;
            } catch (FileNotFoundException) {
                // ignore
            }

            database.Add(packageFullName, new PackageInfo {
                FriendlyName = GetPackageFriendlyName(package),
                RequiredByPackages = new HashSet<string>(),
                Version = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}",
                IsNonRemovable = package.SignatureKind == PackageSignatureKind.System || IsPackageFamilyInUninstallBlocklist(package.Id.FamilyName),
                IsFramework = package.IsFramework,
                InstallLocation = installLocation,
            });
        }

        public static Dictionary<string, PackageInfo> GetPackagesDatabase(bool isAllUsersPackages) {
            // holds information for all packages
            var packagesDatabase = new Dictionary<string, PackageInfo>();

            var packages = isAllUsersPackages ? packageManager.FindPackages() : packageManager.FindPackagesForUser("");

            foreach (var package in packages) {
                var packageFullName = package.Id.FullName;

                // package may have been added already due to creating dependency entries
                if (!packagesDatabase.ContainsKey(packageFullName)) {
                    AddPackageToDatabase(packagesDatabase, packageFullName, package);
                }

                foreach (var dependency in package.Dependencies) {
                    var dependencyFullName = dependency.Id.FullName;

                    // package may have been added already when creating main package entries
                    if (!packagesDatabase.ContainsKey(dependencyFullName)) {
                        AddPackageToDatabase(packagesDatabase, dependencyFullName, dependency);
                    }

                    // record that the dependency is needed for another main package
                    _ = packagesDatabase[dependencyFullName].RequiredByPackages.Add(packageFullName);
                }
            }

            return packagesDatabase;
        }

        public static async Task<int> UninstallPackage(string fullPackageName, bool isAllUsersPackages) {
            var removalOptions = isAllUsersPackages ? RemovalOptions.RemoveForAllUsers : RemovalOptions.None;

            var deploymentOperation = packageManager.RemovePackageAsync(fullPackageName, removalOptions);

            try {
                await deploymentOperation;
            } catch (Exception ex) {
                Console.Error.WriteLine($"error: {fullPackageName} - {ex.Message}");
                return 1;
            }

            if (deploymentOperation.Status == AsyncStatus.Error) {
                var deploymentResult = deploymentOperation.GetResults();
                Console.Error.WriteLine($"error: {fullPackageName} - {deploymentResult.ErrorText}");

                return 1;
            }

            if (deploymentOperation.Status == AsyncStatus.Canceled) {
                Console.Error.WriteLine($"error: {fullPackageName} - removal canceled");
                return 1;
            }

            if (deploymentOperation.Status == AsyncStatus.Completed) {
                return 0;
            }

            return 1;
        }
    }
}
