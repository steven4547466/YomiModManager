using Godot;
using System;

public class ModPanelBundle : ModPanel
{
	public void Init(Mod mod)
	{
		(GetNode("%ModName") as Label).Text = mod.FriendlyName;

		mod.ModPanel = this;
		Mod = mod;
	}
}
