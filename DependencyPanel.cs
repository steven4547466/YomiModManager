using Godot;
using System;

public class DependencyPanel : HBoxContainer
{
	public void Init(string name)
	{
		(GetNode("%DependencyLabel") as Label).Text = name;
	}
}
