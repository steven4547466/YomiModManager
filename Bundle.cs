using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class Bundle
{
    public string Name { get; set; }
    public string FriendlyName { get; set; }
    public string Description { get; set; }
    public string Author { get; set; }
    public string Version { get; set; }
    public List<string> Mods { get; set; }
    public bool Disabled { get; set; } = false;

    [JsonIgnore]
    public BundlePanel BundlePanel { get; set; }
}

