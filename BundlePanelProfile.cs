using Godot;
using System;

public class BundlePanelProfile : BundlePanel
{
	public void Init(Bundle bundle)
	{
		(GetNode("%BundleName") as Label).Text = bundle.FriendlyName;

		bundle.BundlePanel = this;
		Bundle = bundle;
	}
}
