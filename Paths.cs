using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YomiModManager
{
    internal static class Paths
    {
        internal const string RootUrl = "http://mods.yomitussle.tk";
        internal static string RootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YomiModManager");
        internal static string ModsPath = Path.Combine(RootPath, "mods");

        internal static string ConfigPath = Path.Combine(RootPath, "config.json");
        internal static string ManifestPath = Path.Combine(RootPath, "mod_manifest.json");
        internal static string InstalledModsPath = Path.Combine(RootPath, "installed_mods.json");
        internal static string InstalledBundlesPath = Path.Combine(RootPath, "installed_bundles.json");

        internal static string ModProfilesPath = Path.Combine(RootPath, "mod_profiles.json");

        internal static string YomiModsPath 
        { 
            get 
            {
                return Path.Combine(Main.Config.YomiInstallLocation, "mods");
            } 
        }
    }
}
