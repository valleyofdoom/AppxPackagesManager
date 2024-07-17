using System.Collections.Generic;

namespace AppxPackagesManager {
    internal class PackageInfo {
        public string FriendlyName { get; set; }
        public HashSet<string> RequiredByPackages { get; set; }
        public string Version { get; set; }
        public bool IsNonRemovable { get; set; }
        public bool IsFramework { get; set; }
        public string InstallLocation { get; set; }
    }
}
