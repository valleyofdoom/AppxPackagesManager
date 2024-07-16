using System.Runtime.InteropServices;

namespace AppxPackagesManager {
    internal class NativeMethods {
        [DllImport("AppxAllUserStore.dll", CharSet = CharSet.Unicode)]
        public static extern int IsPackageFamilyInUninstallBlocklist(string packageFamilyName, ref bool isPresent);
    }
}
