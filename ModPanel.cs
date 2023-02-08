using Godot;
using System;

public class ModPanel : Panel
{
	public Mod Mod { get; set; }

	public void Init(Mod mod, bool showExtra = true)
	{
		(GetNode("%ModName") as Label).Text = mod.FriendlyName;
		if (showExtra)
		{
			Panel installed = GetNode("%Installed") as Panel;
			if (Main.TryGetInstalledMod(mod.Name, out Mod installedMod))
			{
				installed.Visible = true;
				if (installedMod.Disabled)
					installed.SelfModulate = Color.Color8(255, 0, 0);
			}
			else
			{
				installed.Visible = false;
				installed.SelfModulate = Color.Color8(255, 255, 255);
			}

			(GetNode("%UpdateAvailable") as Panel).Visible = Main.ModHasUpdate(mod.Name);
		} 
		else
		{
			(GetNode("%Installed") as Panel).Visible = false;
			(GetNode("%UpdateAvailable") as Panel).Visible = false;
		}

		mod.ModPanel = this;
		Mod = mod;
	}
}
