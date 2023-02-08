using Godot;
using System;

public class ModIsDependency : Panel
{
	public void InitAndPopup(string mainMod, string dependencyOf, bool uninstall = true)
	{
		(GetNode("%ModIsDependencyLabel") as Label).Text = $"{mainMod} is a dependency of {dependencyOf} and cannot be {(uninstall ? "uninstalled" : "disabled")}. {(uninstall ? "Uninstall" : "Disable")} {dependencyOf} to {(uninstall ? "uninstall" : "disable")} this mod.";
		(GetNode("%UninstallAnywaysButton") as Button).Visible = uninstall;
		(GetNode("%DisableAnywaysButton") as Button).Visible = !uninstall;
		Show();
	}

	public void _on_CloseButton_pressed()
	{
		Hide();
	}
}
