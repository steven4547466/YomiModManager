using Godot;
using System;

public class ModProfilePanel: Panel
{
	public ModProfile ModProfile { get; set; }

	public void Init(ModProfile profile)
	{
		(GetNode("%ProfileName") as Label).Text = profile.Name;

		(GetNode("%Enabled") as Panel).Visible = !profile.Disabled;
		(GetNode("%UpdateAvailable") as Panel).Visible = Main.ProfileHasUpdate(profile.Name);

		profile.ModProfilePanel = this;
		ModProfile = profile;
	}
}
