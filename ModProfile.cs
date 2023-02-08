using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ModProfile
{
    public string Name { get; set; }
    public List<string> Mods { get; set; }
    public List<string> Bundles { get; set; }

    public bool Disabled { get; set; }

    [JsonIgnore]
    public ModProfilePanel ModProfilePanel { get; set; }
}

