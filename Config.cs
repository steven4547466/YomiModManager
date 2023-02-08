using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YomiModManager
{
    public class Config
    {
        public string YomiInstallLocation { get; set; } = string.Empty;
        public bool AutoUpdateClient { get; set; } = false;
        public bool AutoUpdateMods { get; set; } = false;
    }
}
