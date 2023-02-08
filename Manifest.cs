using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YomiModManager;

public class Manifest
{
    public string Version { get; set; }
    public List<Mod> Mods { get; set; }
    public List<Bundle> Bundles { get; set; }
}

