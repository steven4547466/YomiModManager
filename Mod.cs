using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class Mod
{
    public string Name { get; set; }
    public string FriendlyName { get; set; }
    public string Description { get; set; }
    public string Author { get; set; }
    public string Version { get; set; }
    public string Link { get; set; }
    public string Id { get; set; }
    public List<string> Requires { get; set; }
    public bool Overwrites { get; set; }
    public bool ClientSide { get; set; }
    public int Priority { get; set; }

    public List<string> Incompatible { get; set; } = new List<string>();
    public List<string> Tags { get; set; } = new List<string>();

    public bool Disabled { get; set; } = false;

    [JsonIgnore]
    public ModPanel ModPanel { get; set; }

    [JsonIgnore]
    public bool IsLocal { get; set; } = false;
}
