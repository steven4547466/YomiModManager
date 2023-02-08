using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class BundlePanel : Panel
{
	public Bundle Bundle { get; set; }
	public void Init(Bundle bundle)
	{
		(GetNode("%BundleName") as Label).Text = bundle.FriendlyName;
		Panel installed = GetNode("%Installed") as Panel;
		if (Main.TryGetInstalledBundle(bundle.Name, out Bundle installedBundle))
		{
			installed.Visible = true;
			if (installedBundle.Disabled)
				installed.SelfModulate = Color.Color8(255, 0, 0);
		}
		else
		{
			installed.Visible = false;
			installed.SelfModulate = Color.Color8(255, 255, 255);
		}

		(GetNode("%UpdateAvailable") as Panel).Visible = Main.BundleHasUpdate(bundle.Name);
		bundle.BundlePanel = this;
		Bundle = bundle;
	}
}
