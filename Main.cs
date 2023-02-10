using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO.Compression;
using Newtonsoft.Json;

using Directory = System.IO.Directory;
using Path = System.IO.Path;
using File = System.IO.File;
using Environment = System.Environment;
using Newtonsoft.Json.Serialization;
using YomiModManager;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;

public class Main : Panel
{
	public static JsonSerializerSettings JsonSettings = new JsonSerializerSettings
	{
		ContractResolver = new DefaultContractResolver
		{
			NamingStrategy = new SnakeCaseNamingStrategy { ProcessDictionaryKeys = true }
		},
	};

	public static Config Config { get; set; }

	internal bool _ready = false;
	internal static string _latestErrorText;
	internal bool _offline = false;
	public bool Offline
	{
		get
		{
			return _offline;
		}
		set
		{
			if (value)
			{
				(GetNode("%ErrorMessageContainer") as Panel).Visible = true;
				(GetNode("%ErrorMessage") as Label).Text = _latestErrorText;
				(GetNode("%Offline") as Panel).Visible = true;
			}
			else
			{
				(GetNode("%Offline") as Panel).Visible = false;
			}
			_offline = value;
		}
	}

	public static List<string> CurrentFilterTags { get; set; } = new List<string>();

	public static Manifest Manifest { get; set; }
	public static List<Mod> InstalledMods { get; set; } = new List<Mod>();
	public static List<Bundle> InstalledBundles { get; set; } = new List<Bundle>();

	public static List<ModProfile> ModProfiles { get; set; } = new List<ModProfile>();
	public static ModProfilePanel SelectedProfile { get; set; }

	public static StyleBoxFlat DefaultTabPanelStyle { get; set; }
	public static Panel SelectedTab { get; set; }

	public static StyleBoxFlat DefaultModPanelStyle { get; set; }
	public static ModPanel SelectedMod { get; set; }

	public static BundlePanel SelectedBundle { get; set; }

	public static string ModToUploadPath { get; set; }
	public static Mod ModToUpload { get; set; }

	public static Bundle BundleToUpload { get; set; }

	public static ModProfile ProfileToCreate { get; set; }

	public static bool TryGetMod(string name, out Mod mod)
	{
		foreach (Mod m in Manifest.Mods)
		{
			if (m.Name == name)
			{
				mod = m;
				return true;
			}
		}

		mod = null;
		return false;
	}

	public static bool TryGetInstalledMod(string name, out Mod mod)
	{
		foreach (Mod m in InstalledMods)
		{
			if (m.Name == name)
			{
				mod = m;
				return true;
			}
		}

		mod = null;
		return false;
	}

	public static bool ModInstalled(string name)
	{
		return InstalledMods.Any(mod => mod.Name == name);
	}

	public static bool ModHasUpdate(string name)
	{
		Mod manifestMod = Manifest.Mods.FirstOrDefault(mod => mod.Name == name);
		if (manifestMod == null)
			return false;

		Mod installedMod = InstalledMods.FirstOrDefault(mod => mod.Name == name);
		if (installedMod == null)
			return false;

		return installedMod.Version != manifestMod.Version;
	}

	public void AddInstalledMod(string path, bool andEnable = true)
	{
		if (!Directory.Exists(Paths.YomiModsPath))
		{
			return;
		}

		string name = string.Empty;
		using (var file = File.OpenRead(path))
		using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
		{
			foreach (var entry in zip.Entries)
			{
				if (entry.Name == "_metadata")
				{
					using (StreamReader sr = new StreamReader(entry.Open()))
					{
						string json = sr.ReadToEnd();
						Mod mod = JsonConvert.DeserializeObject<Mod>(json, JsonSettings);
						name = mod.Name;
						if (ModInstalled(mod.Name))
						{
							InstalledMods.RemoveAt(InstalledMods.FindIndex(m => m.Name == mod.Name));
							InstalledMods.Add(mod);
						} 
						else
						{
							InstalledMods.Add(mod);
						}

						if (!andEnable)
							mod.Disabled = true;
						
						File.WriteAllText(Paths.InstalledModsPath, JsonConvert.SerializeObject(InstalledMods, JsonSettings));

						if (_ready)
							SetupMods((GetNode("%SearchBar") as LineEdit).Text);
					}
					break;
				}
			}
		}

		if (andEnable)
			File.Copy(path, Path.Combine(Paths.YomiModsPath, $"{name}.zip"), true);
	}

	public void InstallMod(string name, bool andEnable = true)
	{
		if (!Directory.Exists(Paths.YomiModsPath))
		{
			return;
		}

		if (TryGetMod(name, out Mod mod))
		{
			if (Offline)
				return;
			using (WebClient wc = new WebClient())
			{
				try
				{
					RecurseAndDownloadDependencies(mod, new List<string>(), andEnable);
					string path = Path.Combine(Paths.ModsPath, $"{mod.Name}.zip");
					wc.DownloadFile($"{Paths.RootUrl}/mod/{mod.Name}", path);
					AddInstalledMod(path, andEnable);
				}
				catch (Exception ex)
				{
					GD.PrintErr(ex);
					_latestErrorText = ex.Message + "\n" + ex.StackTrace;
					Offline = true;
				}
			}
		}
	}

	public void UninstallMod(string name, bool force = false, bool noDialogue = false)
	{
		if (!Directory.Exists(Paths.YomiModsPath))
		{
			return;
		}

		if (TryGetInstalledMod(name, out Mod mod))
		{
			if (!force)
			{
				foreach (Mod m in InstalledMods)
				{
					if (m.Requires.Contains(mod.Name))
					{
						if (!noDialogue)
							(GetNode("%ModIsDependency") as ModIsDependency).InitAndPopup(mod.FriendlyName, m.FriendlyName);
						return;
					}
				}
			}

			string path = Path.Combine(Paths.ModsPath, $"{mod.Name}.zip");
			if (File.Exists(path))
				File.Delete(path);

			if (File.Exists(Path.Combine(Paths.YomiModsPath, $"{mod.Name}.zip")))
				File.Delete(Path.Combine(Paths.YomiModsPath, $"{mod.Name}.zip"));

			InstalledMods.Remove(mod);
			File.WriteAllText(Paths.InstalledModsPath, JsonConvert.SerializeObject(InstalledMods, JsonSettings));

			if (mod.Requires.Count > 0 && !(mod.Requires.Count == 1 && mod.Requires[0] == string.Empty))
			{
				List<string> depsUnnecessaryAfterUninstall = mod.Requires;
				List<string> allDepsExceptThis = new List<string>();
				foreach (Mod m in InstalledMods)
				{
					if (m != mod)
					{
						if (m.Requires.Count > 0 && !(m.Requires.Count == 1 && m.Requires[0] == string.Empty))
							allDepsExceptThis.AddRange(m.Requires);
					}
				}

				depsUnnecessaryAfterUninstall = depsUnnecessaryAfterUninstall.Where(n => !allDepsExceptThis.Contains(n)).ToList();

				foreach (string dep in depsUnnecessaryAfterUninstall)
				{
					UninstallMod(dep);
				}
			}

			ClearModInfo();
			SetupMods((GetNode("%SearchBar") as LineEdit).Text);
		}
	}

	public void DisableMod(string name, bool force = false, bool noDialogue = false, bool andDependencies = false)
	{
		if (!Directory.Exists(Paths.YomiModsPath))
		{
			return;
		}

		if (TryGetInstalledMod(name, out Mod mod))
		{
			if (mod.Disabled)
				return;
			if (!force)
			{
				foreach (Mod m in InstalledMods)
				{
					if (!m.Disabled && m.Requires.Contains(mod.Name))
					{
						if (!noDialogue)
							(GetNode("%ModIsDependency") as ModIsDependency).InitAndPopup(mod.FriendlyName, m.FriendlyName, false);
						return;
					}
				}
			}

			if (andDependencies)
			{
				foreach (string m in mod.Requires)
				{
					DisableMod(m, force, noDialogue, andDependencies);
				}
			}

			mod.Disabled = true;

			if (File.Exists(Path.Combine(Paths.YomiModsPath, $"{mod.Name}.zip")))
				File.Delete(Path.Combine(Paths.YomiModsPath, $"{mod.Name}.zip"));

			File.WriteAllText(Paths.InstalledModsPath, JsonConvert.SerializeObject(InstalledMods, JsonSettings));
			SetupMods((GetNode("%SearchBar") as LineEdit).Text);
		}
	}

	public void EnableMod(string name)
	{
		if (!Directory.Exists(Paths.YomiModsPath))
		{
			return;
		}

		if (TryGetInstalledMod(name, out Mod mod))
		{
			if (!mod.Disabled)
				return;
			if (mod.Requires.Count > 0 && !(mod.Requires.Count == 1 && mod.Requires[0] == string.Empty))
			{
				foreach (string dep in mod.Requires)
				{
					if (TryGetInstalledMod(dep, out Mod depMod))
					{
						if (depMod.Disabled)
							EnableMod(dep);
					} 
					else
					{
						InstallMod(dep);
					}
				}
			}

			mod.Disabled = false;

			File.Copy(Path.Combine(Paths.ModsPath, $"{mod.Name}.zip"), Path.Combine(Paths.YomiModsPath, $"{mod.Name}.zip"), true);

			File.WriteAllText(Paths.InstalledModsPath, JsonConvert.SerializeObject(InstalledMods, JsonSettings));
			SetupMods((GetNode("%SearchBar") as LineEdit).Text);
		}
	}

	#region Bundles
	public static bool TryGetBundle(string name, out Bundle bundle)
	{
		foreach (Bundle b in Manifest.Bundles)
		{
			if (b.Name == name)
			{
				bundle = b;
				return true;
			}
		}

		bundle = null;
		return false;
	}

	public static bool TryGetInstalledBundle(string name, out Bundle bundle)
	{
		foreach (Bundle b in InstalledBundles)
		{
			if (b.Name == name)
			{
				bundle = b;
				return true;
			}
		}

		bundle = null;
		return false;
	}

	public static bool BundleInstalled(string name)
	{
		return InstalledBundles.Any(bundle => bundle.Name == name);
	}

	public static bool BundleHasUpdate(string name)
	{
		Bundle manifestBundle = Manifest.Bundles.FirstOrDefault(bundle => bundle.Name == name);
		if (manifestBundle == null)
			return false;

		Bundle installedBundle = InstalledBundles.FirstOrDefault(bundle => bundle.Name == name);
		if (installedBundle == null)
			return false;

		if (installedBundle.Version != manifestBundle.Version)
			return true;

		foreach (string mod in installedBundle.Mods)
		{
			if (!ModInstalled(mod) || ModHasUpdate(mod))
				return true;
		}

		return false;
	}

	public void InstallBundle(string name, bool andEnable = true)
	{
		if (TryGetBundle(name, out Bundle bundle))
		{
			if (BundleInstalled(bundle.Name))
			{
				InstalledBundles.RemoveAt(InstalledBundles.FindIndex(m => m.Name == bundle.Name));
				InstalledBundles.Add(bundle);
			}
			else
			{
				InstalledBundles.Add(bundle);
			}

			File.WriteAllText(Paths.InstalledBundlesPath, JsonConvert.SerializeObject(InstalledBundles, JsonSettings));

			foreach (string mod in bundle.Mods)
			{
				InstallMod(mod, andEnable);
			}

			bundle.Disabled = !andEnable;

			SetupBundles((GetNode("%SearchBar") as LineEdit).Text);
		}
	}

	public void UninstallBundle(string name, bool force = false)
	{
		if (TryGetInstalledBundle(name, out Bundle bundle))
		{
			InstalledBundles.RemoveAt(InstalledBundles.FindIndex(m => m.Name == bundle.Name));
			File.WriteAllText(Paths.InstalledBundlesPath, JsonConvert.SerializeObject(InstalledBundles, JsonSettings));

			foreach (string mod in bundle.Mods)
			{
				UninstallMod(mod, force, true);
			}

			ClearBundleInfo();
			SetupBundles((GetNode("%SearchBar") as LineEdit).Text);
		}
	}

	public void DisableBundle(string name, bool force = false)
	{
		if (TryGetInstalledBundle(name, out Bundle bundle))
		{
			foreach(string mod in bundle.Mods)
			{
				DisableMod(mod, force, true, true);
			}

			bundle.Disabled = true;

			File.WriteAllText(Paths.InstalledBundlesPath, JsonConvert.SerializeObject(InstalledBundles, JsonSettings));
			SetupBundles((GetNode("%SearchBar") as LineEdit).Text);
		}
	}

	public void EnableBundle(string name)
	{
		if (TryGetInstalledBundle(name, out Bundle bundle))
		{
			foreach (string mod in bundle.Mods)
			{
				if (ModInstalled(mod))
					EnableMod(mod);
				else
					InstallMod(mod);
			}
			bundle.Disabled = false;

			File.WriteAllText(Paths.InstalledBundlesPath, JsonConvert.SerializeObject(InstalledBundles, JsonSettings));
			SetupBundles((GetNode("%SearchBar") as LineEdit).Text);
		}
	}
	#endregion

	#region Profiles
	public static bool TryGetProfile(string name, out ModProfile profile)
	{
		profile = ModProfiles.Find(p => p.Name == name);
		return profile != null;
	}

	public static bool ProfileEnabled(string name)
	{
		if (TryGetProfile(name, out ModProfile profile))
		{
			return !profile.Disabled;
		}

		return false;
	}

	public static bool ProfileHasUpdate(string name)
	{
		if (TryGetProfile(name, out ModProfile profile))
		{
			foreach (string mod in profile.Mods)
			{
				if (!ModInstalled(mod) || ModHasUpdate(mod))
					return true;
			}

			foreach (string bundle in profile.Bundles)
			{
				if (!BundleInstalled(bundle) || BundleHasUpdate(bundle))
					return true;
			}
		}

		return false;
	}

	public void UpdateProfile(string name)
	{
		if (TryGetProfile(name, out ModProfile profile))
		{
			if (!profile.Disabled)
			{
				EnableProfile(profile.Name);
			} 
			else
			{
				foreach (string mod in profile.Mods)
				{
					if (!ModInstalled(mod) || ModHasUpdate(mod))
					{
						InstallMod(mod, false);
						//DisableMod(mod);
					}
				}

				foreach (string bundle in profile.Bundles)
				{
					if (!BundleInstalled(bundle) || BundleHasUpdate(bundle))
					{
						InstallBundle(bundle, false);
						//DisableBundle(bundle);
					}
				}
			}

			SetupProfiles((GetNode("%SearchBar") as LineEdit).Text);
		}
	}

	public void EnableProfile(string name)
	{
		if (TryGetProfile(name, out ModProfile profile))
		{
			foreach (Mod mod in InstalledMods)
			{
				DisableMod(mod.Name, true, true, true);
			}

			foreach (Bundle bundle in InstalledBundles)
			{
				DisableBundle(bundle.Name, true);
			}

			profile.Disabled = false;

			foreach (ModProfile p in ModProfiles)
			{
				if (!p.Disabled)
				{
					foreach (string mod in p.Mods)
					{
						if (!ModInstalled(mod) || ModHasUpdate(mod))
							InstallMod(mod);
						else
							EnableMod(mod);
					}

					foreach (string bundle in p.Bundles)
					{
						if (!BundleInstalled(bundle) || BundleHasUpdate(bundle))
							InstallBundle(bundle);
						else
							EnableBundle(bundle);
					}
				}
			}

			SetupProfiles((GetNode("%SearchBar") as LineEdit).Text);
		}
	}

	public void DisableProfile(string name)
	{
		if (TryGetProfile(name, out ModProfile profile))
		{
			if (profile.Disabled)
				return;

			foreach (string mod in profile.Mods)
			{
				DisableMod(mod, true, true, true);
			}

			foreach (string bundle in profile.Bundles)
			{
				DisableBundle(bundle, true);
			}

			profile.Disabled = true;

			SetupProfiles((GetNode("%SearchBar") as LineEdit).Text);
		}
	}

	public void DeleteProfile(string name)
	{
		if (TryGetProfile(name, out ModProfile profile))
		{
			ModProfiles.Remove(profile);
			File.WriteAllText(Paths.ModProfilesPath, JsonConvert.SerializeObject(ModProfiles, JsonSettings));
			SetupProfiles((GetNode("%SearchBar") as LineEdit).Text);
			ClearProfileInfo();
		}
	}
	#endregion

	public static bool IsModLocal(string mod)
	{
		return Manifest.Mods.Find(m => m.Name == mod) == null;
	}

	public async override void _Ready()
	{
		try
		{
			OS.SetWindowTitle("Yomi Mod Manager");

			string workingPath = AppDomain.CurrentDomain.BaseDirectory;

			if (!Directory.Exists(Paths.RootPath))
				Directory.CreateDirectory(Paths.RootPath);

			if (!Directory.Exists(Paths.ModsPath))
				Directory.CreateDirectory(Paths.ModsPath);

			if (!File.Exists(Paths.ConfigPath))
			{
				Config = new Config();
				File.WriteAllText(Paths.ConfigPath, JsonConvert.SerializeObject(Config, JsonSettings));
			}
			else
			{
				Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Paths.ConfigPath), JsonSettings);
			}

			using (WebClient wc = new WebClient())
			{
				try
				{
					string currentVersion = "0";

					if (File.Exists(Path.Combine(workingPath, "buildid.txt")))
						currentVersion = File.ReadAllText(Path.Combine(workingPath, "buildid.txt")).Trim();

					wc.Encoding = Encoding.UTF8;
					string version = wc.DownloadString($"{Paths.RootUrl}/client_version").Trim();
					if (!string.IsNullOrEmpty(version) && currentVersion != version)
					{
						if (!Config.AutoUpdateClient)
							(GetNode("%UpdateAvailable") as Popup).PopupCentered();
						else
							UpdateClient();
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr(ex);
					_latestErrorText = ex.Message + "\n" + ex.StackTrace;
					Offline = true;
				}

			}

			bool showPopup = false;

			try
			{
				if (Config.YomiInstallLocation == string.Empty || !Directory.Exists(Paths.YomiModsPath))
				{
					await ToSignal(GetTree(), "idle_frame");
					showPopup = true;
					FileDialog fileDialogue = GetNode("%SelectYomiLocation") as FileDialog;
					fileDialogue.PopupCentered();

					object[] result = await ToSignal(fileDialogue, "file_selected");
					string path = Path.GetDirectoryName((string)result[0]);

					GD.Print("Selected: " + path);

					Config.YomiInstallLocation = path;
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr(ex);
			}


			if (showPopup)
			{
				(GetNode("%HelpPopup") as Popup).Show();
				File.WriteAllText(Paths.ConfigPath, JsonConvert.SerializeObject(Config, JsonSettings));
			}

			DefaultModPanelStyle = new StyleBoxFlat();
			DefaultModPanelStyle.BgColor = Godot.Color.Color8(17, 19, 31);

			DefaultTabPanelStyle = new StyleBoxFlat();
			DefaultTabPanelStyle.BgColor = Godot.Color.Color8(29, 31, 43);

			SelectedTab = GetNode("%AllModsTab") as Panel;

			if (File.Exists(Paths.InstalledModsPath))
			{
				string installedMods = File.ReadAllText(Paths.InstalledModsPath);
				InstalledMods = JsonConvert.DeserializeObject<List<Mod>>(installedMods, JsonSettings);
			}

			if (File.Exists(Paths.InstalledBundlesPath))
			{
				string installedBundles = File.ReadAllText(Paths.InstalledBundlesPath);
				InstalledBundles = JsonConvert.DeserializeObject<List<Bundle>>(installedBundles, JsonSettings);
			}

			if (File.Exists(Paths.ModProfilesPath))
			{
				string profiles = File.ReadAllText(Paths.ModProfilesPath);
				ModProfiles = JsonConvert.DeserializeObject<List<ModProfile>>(profiles, JsonSettings);
			}

			ShowInfoPanel("ModInfoPanel");
			DownloadManifest();
			DetectLocalMods();

			if (Config.AutoUpdateMods)
			{
				List<Mod> modsToUpdate = new List<Mod>();
				foreach (Mod mod in InstalledMods)
				{
					if (ModHasUpdate(mod.Name))
					{
						modsToUpdate.Add(mod);
					}
				}

				foreach(Mod mod in modsToUpdate)
                {
					InstallMod(mod.Name, !mod.Disabled);
				}
			}

			foreach (Button button in GetNode("%TagsFilterCheckBoxContainer").GetChildren())
			{
				button.Connect("toggled", this, "TagFilterUpdated", new Godot.Collections.Array(button));
			}

			SetupMods();
			_ready = true;
		} 
		catch(Exception ex)
		{
			GD.PrintErr(ex);
			_latestErrorText = ex.Message + "\n" + ex.StackTrace;
			Offline = true;
		}
	}

	public void TagFilterUpdated(bool pressed, Button button)
	{
		string tag = button.Text;

		if (pressed)
		{
			CurrentFilterTags.Add(tag);
		} 
		else
		{
			CurrentFilterTags.Remove(tag);
		}

		if (SelectedTab.Name == "AllModsTab" || SelectedTab.Name == "InstalledModsTab")
			SetupMods((GetNode("%SearchBar") as LineEdit).Text);
		else if (SelectedTab.Name == "BundlesTab" || SelectedTab.Name == "InstalledBundlesTab")
			SetupBundles((GetNode("%SearchBar") as LineEdit).Text);
		else
			SetupProfiles((GetNode("%SearchBar") as LineEdit).Text);
	}

	public void DetectLocalMods()
	{
		if (!Directory.Exists(Paths.YomiModsPath))
		{
			return;
		}

		foreach (string path in Directory.GetFiles(Paths.YomiModsPath, "*.zip"))
		{
			try
			{
				Mod mod = GetModFromZip(path);
				if (!ModInstalled(mod.Name))
				{
					InstalledMods.Add(mod);
					File.WriteAllText(Paths.InstalledModsPath, JsonConvert.SerializeObject(InstalledMods, JsonSettings));
					File.Copy(path, Path.Combine(Paths.ModsPath, $"{mod.Name}.zip"), true);
					File.Move(path, Path.Combine(Paths.YomiModsPath, $"{mod.Name}.zip"));
				}
			} catch (Exception ex) { GD.PrintErr(ex); }
		}

		List<Mod> modsToRemove = new List<Mod>();

		foreach (Mod mod in InstalledMods)
		{
			mod.IsLocal = IsModLocal(mod.Name);
			if (mod.IsLocal)
			{
				if (!File.Exists(Path.Combine(Paths.ModsPath, $"{mod.Name}.zip")))
				{
					modsToRemove.Add(mod);
				}
				
				if (!File.Exists(Path.Combine(Paths.YomiModsPath, $"{mod.Name}.zip")))
				{
					mod.Disabled = true;
				}
			}
		}

		foreach (Mod mod in modsToRemove)
		{
			InstalledMods.Remove(mod);
		}
	}

	public void DownloadManifest()
	{
		GD.Print("Downloading manifest");
		bool shouldDownload = false;

		if (File.Exists(Paths.ManifestPath))
		{
			GD.Print("Manifest exists");
			string manifest = File.ReadAllText(Paths.ManifestPath);
			Manifest = JsonConvert.DeserializeObject<Manifest>(manifest, JsonSettings);
			if (Manifest != null)
			{
				using (WebClient wc = new WebClient())
				{
					try
					{
						GD.Print("Getting manifest version");
						wc.Encoding = Encoding.UTF8;
						string version = wc.DownloadString($"{Paths.RootUrl}/manifest_version");
						if (!string.IsNullOrEmpty(version) && Manifest.Version != version)
						{
							shouldDownload = true;
						}
					}
					catch(Exception ex)
					{
						GD.PrintErr(ex);
						_latestErrorText = ex.Message + "\n" + ex.StackTrace;
						Offline = true;
					}
					
				}
			}
		}
		else
		{
			GD.Print("Manifest doesn't exist");
			shouldDownload = true;
		}

		if (!Offline && shouldDownload)
		{
			GD.Print("Attempting download");
			using (WebClient wc = new WebClient())
			{
				try
				{
					wc.Encoding = Encoding.UTF8;
					GD.Print("Downloading");
					string manifest = wc.DownloadString($"{Paths.RootUrl}/mod_manifest");
					Manifest = JsonConvert.DeserializeObject<Manifest>(manifest, JsonSettings);
					File.WriteAllText(Paths.ManifestPath, manifest);
					GD.Print("Written");
				} 
				catch (Exception ex)
				{
					GD.PrintErr(ex);
					_latestErrorText = ex.Message + "\n" + ex.StackTrace;
					Offline = true;
				}
				
			}
		}
	}

	public static Mod GetModFromZip(string path)
	{
		using (var file = File.OpenRead(path))
		using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
		{
			foreach (var entry in zip.Entries)
			{
				if (entry.Name == "_metadata")
				{
					using (StreamReader sr = new StreamReader(entry.Open()))
					{

						try
						{
							string json = Regex.Replace(sr.ReadToEnd(), "\"priority\": ?(\\d+),\n?}", "\"priority\": $1\n}");
							return JsonConvert.DeserializeObject<Mod>(json, JsonSettings);
						}
						catch (Exception ex)
						{
							throw ex;
						}
					}
				}
			}
		}

		return null;
	}

	public void SetupMods(string filter = "")
	{
		bool showOnlyInstalled = SelectedTab.Name == "InstalledModsTab";

		string previouslySelected = string.Empty;
		if (SelectedMod != null)
		{
			previouslySelected = SelectedMod.Mod.Name;
			SelectedMod = null;
		}
		VBoxContainer modsList = GetNode("%ModsList") as VBoxContainer;

		foreach (Node node in modsList.GetChildren())
		{
			modsList.RemoveChild(node);
			node.QueueFree();
		}

		List<Mod> mods = showOnlyInstalled ? InstalledMods : Manifest.Mods;

		ModPanel prevSelect = null;

		foreach (Mod mod in mods)
		{
			if (filter != string.Empty)
			{
				if (!mod.FriendlyName.StartsWith(filter, StringComparison.OrdinalIgnoreCase) &&
					(filter.Length < 3 || mod.FriendlyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) == -1)
					)
				{
					continue;
				}
			}

			if (CurrentFilterTags.Count > 0 && !mod.Tags.Any(t => CurrentFilterTags.Contains(t)))
			{
				continue;
			}

			PackedScene modPanel = ResourceLoader.Load("res://ModPanel.tscn") as PackedScene;
			ModPanel panel = modPanel.Instance() as ModPanel;
			panel.Connect("gui_input", this, "OnModClicked", new Godot.Collections.Array(panel));
			RecurseAndConnectInput(panel.GetChildren(), panel);
			panel.Init(mod);
			modsList.AddChild(panel);
			if (mod.Name == previouslySelected)
			{
				prevSelect = panel;
			}
		}
		
		if (prevSelect != null)
		{
			OnModClicked(null, prevSelect);
		}
	}

	public void SetupBundles(string filter = "")
	{
		bool showOnlyInstalled = SelectedTab.Name == "InstalledBundlesTab";

		string previouslySelected = string.Empty;
		if (SelectedBundle != null)
		{
			previouslySelected = SelectedBundle.Bundle.Name;
			SelectedBundle = null;
		}
		VBoxContainer bundlesList = GetNode("%ModsList") as VBoxContainer;

		foreach (Node node in bundlesList.GetChildren())
		{
			bundlesList.RemoveChild(node);
			node.QueueFree();
		}

		List<Bundle> bundles = showOnlyInstalled ? InstalledBundles : Manifest.Bundles;

		BundlePanel prevSelect = null;

		foreach (Bundle bundle in bundles)
		{
			if (filter != string.Empty)
			{
				if (!bundle.FriendlyName.StartsWith(filter, StringComparison.OrdinalIgnoreCase) &&
					(filter.Length < 3 || bundle.FriendlyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) == -1)
					)
				{
					continue;
				}
			}

			if (CurrentFilterTags.Count > 0)
			{
				bool skip = true;

				foreach (string m in bundle.Mods)
				{
					Mod mod;
					if (TryGetMod(m, out mod))
					{
						if (mod.Tags.Any(t => CurrentFilterTags.Contains(t)))
						{
							skip = false;
							break;
						}
					}
					else if (TryGetInstalledMod(m, out mod))
					{
						if (mod.Tags.Any(t => CurrentFilterTags.Contains(t)))
						{
							skip = false;
							break;
						}
					}
				}

				if (skip)
					continue;
			}

			PackedScene modPanel = ResourceLoader.Load("res://BundlePanel.tscn") as PackedScene;
			BundlePanel panel = modPanel.Instance() as BundlePanel;
			panel.Connect("gui_input", this, "OnBundleClicked", new Godot.Collections.Array(panel));
			RecurseAndConnectInput(panel.GetChildren(), panel);
			panel.Init(bundle);
			bundlesList.AddChild(panel);
			if (bundle.Name == previouslySelected)
			{
				prevSelect = panel;
			}
		}

		if (prevSelect != null)
		{
			OnBundleClicked(null, prevSelect);
		}
	}

	public void SetupProfiles(string filter = "")
	{
		string previouslySelected = string.Empty;
		if (SelectedProfile != null)
		{
			previouslySelected = SelectedProfile.ModProfile.Name;
			SelectedProfile = null;
		}

		VBoxContainer profilesList = GetNode("%ModsList") as VBoxContainer;

		foreach (Node node in profilesList.GetChildren())
		{
			profilesList.RemoveChild(node);
			node.QueueFree();
		}

		ModProfilePanel prevSelect = null;

		foreach (ModProfile profile in ModProfiles)
		{
			if (filter != string.Empty)
			{
				if (!profile.Name.StartsWith(filter, StringComparison.OrdinalIgnoreCase) &&
					(filter.Length < 3 || profile.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) == -1)
					)
				{
					continue;
				}
			}

			if (CurrentFilterTags.Count > 0)
			{
				bool skip = true;

				foreach (string m in profile.Mods)
				{
					Mod mod;
					if (TryGetMod(m, out mod))
					{
						if (mod.Tags.Any(t => CurrentFilterTags.Contains(t)))
						{
							skip = false;
							break;
						}
					}
				}

				if (skip)
				{
					foreach (string b in profile.Bundles)
					{
						if (!skip)
							break;

						if (TryGetBundle(b, out Bundle bundle))
						{
							foreach (string m in bundle.Mods)
							{
								Mod mod;
								if (TryGetMod(m, out mod))
								{
									if (mod.Tags.Any(t => CurrentFilterTags.Contains(t)))
									{
										skip = false;
										break;
									}
								}
								else if (TryGetInstalledMod(m, out mod))
								{
									if (mod.Tags.Any(t => CurrentFilterTags.Contains(t)))
									{
										skip = false;
										break;
									}
								}
							}
						}
					}
				}

				if (skip)
					continue;

			}

			PackedScene profilePanel = ResourceLoader.Load("res://ModProfilePanel.tscn") as PackedScene;
			ModProfilePanel panel = profilePanel.Instance() as ModProfilePanel;
			panel.Connect("gui_input", this, "OnProfileClicked", new Godot.Collections.Array(panel));
			RecurseAndConnectInput(panel.GetChildren(), panel);
			panel.Init(profile);
			profilesList.AddChild(panel);
			if (profile.Name == previouslySelected)
			{
				prevSelect = panel;
			}
		}

		if (prevSelect != null)
		{
			OnProfileClicked(null, prevSelect);
		}
	}

	public void RecurseAndConnectInput(Godot.Collections.Array children, ModPanel panel)
	{
		foreach (Node child in children)
		{
			child.Connect("gui_input", this, "OnModClicked", new Godot.Collections.Array(panel));

			Godot.Collections.Array more = child.GetChildren();
			if (more.Count > 0)
				RecurseAndConnectInput(more, panel);
		}
	}

	public void RecurseAndConnectInput(Godot.Collections.Array children, BundlePanel panel)
	{
		foreach (Node child in children)
		{
			child.Connect("gui_input", this, "OnBundleClicked", new Godot.Collections.Array(panel));

			Godot.Collections.Array more = child.GetChildren();
			if (more.Count > 0)
				RecurseAndConnectInput(more, panel);
		}
	}

	public void RecurseAndConnectInput(Godot.Collections.Array children, ModProfilePanel panel)
	{
		foreach (Node child in children)
		{
			child.Connect("gui_input", this, "OnProfileClicked", new Godot.Collections.Array(panel));

			Godot.Collections.Array more = child.GetChildren();
			if (more.Count > 0)
				RecurseAndConnectInput(more, panel);
		}
	}

	public void RecurseAndConnectInputBundleUpload(Godot.Collections.Array children, ModPanel panel)
	{
		foreach (Node child in children)
		{
			child.Connect("gui_input", this, "OnModClickedBundleUpload", new Godot.Collections.Array(panel));

			Godot.Collections.Array more = child.GetChildren();
			if (more.Count > 0)
				RecurseAndConnectInputBundleUpload(more, panel);
		}
	}

	public void RecurseAndConnectInputProfileCreator(Godot.Collections.Array children, ModPanel panel)
	{
		foreach (Node child in children)
		{
			child.Connect("gui_input", this, "OnModClickedProfileCreator", new Godot.Collections.Array(panel));

			Godot.Collections.Array more = child.GetChildren();
			if (more.Count > 0)
				RecurseAndConnectInputProfileCreator(more, panel);
		}
	}

	public void RecurseAndConnectInputProfileCreator(Godot.Collections.Array children, BundlePanel panel)
	{
		foreach (Node child in children)
		{
			child.Connect("gui_input", this, "OnBundleClickedProfileCreator", new Godot.Collections.Array(panel));

			Godot.Collections.Array more = child.GetChildren();
			if (more.Count > 0)
				RecurseAndConnectInputProfileCreator(more, panel);
		}
	}

	public void ClearModInfo()
	{
		(GetNode("%ModName") as Label).Text = string.Empty;
		(GetNode("%Installed") as Panel).Visible = false;
		(GetNode("%ServerSided") as Panel).Visible = false;
		(GetNode("%LocalMod") as Panel).Visible = false;
		(GetNode("%VersionLabel") as Label).Text = string.Empty;

		HBoxContainer authorsContainer = GetNode("%AuthorsContainer") as HBoxContainer;

		foreach (Node node in authorsContainer.GetChildren())
		{
			authorsContainer.RemoveChild(node);
			node.QueueFree();
		}

		VBoxContainer dependenciesContainer = GetNode("%DependenciesContainer") as VBoxContainer;
		foreach (Node node in dependenciesContainer.GetChildren())
		{
			dependenciesContainer.RemoveChild(node);
			node.QueueFree();
		}

		VBoxContainer tagsContainer = GetNode("%ModTagsContainer") as VBoxContainer;
		foreach (Node node in tagsContainer.GetChildren())
		{
			tagsContainer.RemoveChild(node);
			node.QueueFree();
		}

		(GetNode("%Description") as Label).Text = string.Empty;
		(GetNode("%CurrentlyInstalledVersion") as Label).Visible = false;
		(GetNode("%InstallButton") as Button).Visible = true;
		(GetNode("%DeleteButton") as Button).Visible = true;
		(GetNode("%UninstallButton") as Button).Visible = false;
		(GetNode("%DisableButton") as Button).Visible = false;
		(GetNode("%EnableButton") as Button).Visible = false;
		(GetNode("%UpdateModButton") as Button).Visible = false;
	}

	public void ClearBundleInfo()
	{
		(GetNode("%BundleName") as Label).Text = string.Empty;
		(GetNode("%BundleInstalled") as Panel).Visible = false;
		(GetNode("%BundleVersionLabel") as Label).Text = string.Empty;

		HBoxContainer authorsContainer = GetNode("%BundleAuthorsContainer") as HBoxContainer;

		foreach (Node node in authorsContainer.GetChildren())
		{
			authorsContainer.RemoveChild(node);
			node.QueueFree();
		}

		VBoxContainer modsContainer = GetNode("%ModsContainer") as VBoxContainer;
		foreach (Node node in modsContainer.GetChildren())
		{
			modsContainer.RemoveChild(node);
			node.QueueFree();
		}

		(GetNode("%BundleDescription") as Label).Text = string.Empty;
		(GetNode("%BundleCurrentlyInstalledVersion") as Label).Visible = false;
		(GetNode("%BundleInstallButton") as Button).Visible = true;
		(GetNode("%BundleDeleteButton") as Button).Visible = true;
		(GetNode("%BundleUninstallButton") as Button).Visible = false;
		(GetNode("%BundleDisableButton") as Button).Visible = false;
		(GetNode("%BundleEnableButton") as Button).Visible = false;
		(GetNode("%UpdateBundleButton") as Button).Visible = false;
	}

	public void ClearProfileInfo()
	{
		(GetNode("%ProfileName") as Label).Text = string.Empty;

		(GetNode("%ProfileDeleteButton") as Button).Visible = true;
		(GetNode("%ProfileEnableButton") as Button).Visible = false;
		(GetNode("%ProfileUpdateModButton") as Button).Visible = false;
		(GetNode("%ProfileDisableButton") as Button).Visible = false;

		HBoxContainer authorsContainer = GetNode("%ProfileAuthorsContainer") as HBoxContainer;

		foreach (Node node in authorsContainer.GetChildren())
		{
			authorsContainer.RemoveChild(node);
			node.QueueFree();
		}

		VBoxContainer modsContainer = GetNode("%ProfileModsContainer") as VBoxContainer;
		foreach (Node node in modsContainer.GetChildren())
		{
			modsContainer.RemoveChild(node);
			node.QueueFree();
		}

		VBoxContainer bundlesContainer = GetNode("%ProfileBundlesContainer") as VBoxContainer;
		foreach (Node node in bundlesContainer.GetChildren())
		{
			bundlesContainer.RemoveChild(node);
			node.QueueFree();
		}
	}

	public void OnModClicked(InputEvent inputEvent, ModPanel modPanel)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			if (SelectedMod != null)
			{
				if (SelectedMod == modPanel)
					return;

				//if (SelectedMod.HasStyleboxOverride("panel"))
				//	SelectedMod.RemoveStyleboxOverride("panel");
				SelectedMod.AddStyleboxOverride("panel", DefaultModPanelStyle);
			}

			AcceptEvent();

			StyleBoxFlat style = new StyleBoxFlat();
			style.BgColor = Godot.Color.Color8(17, 19, 31);
			style.BorderColor = Godot.Color.Color8(93, 170, 243);
			style.SetBorderWidthAll(3);
			//if (SelectedMod.HasStyleboxOverride("panel"))
			//	SelectedMod.RemoveStyleboxOverride("panel");
			modPanel.AddStyleboxOverride("panel", style);

			ClearModInfo();

			Mod mod = modPanel.Mod;
			Label modName = (GetNode("%ModName") as Label);
			modName.Text = mod.FriendlyName;
			modName.HintTooltip = mod.FriendlyName;
			(GetNode("%ServerSided") as Panel).Visible = !mod.ClientSide;
			(GetNode("%VersionLabel") as Label).Text = $"Version: {mod.Version}";

			Panel installed = GetNode("%Installed") as Panel;
			if (TryGetInstalledMod(mod.Name, out Mod installedMod))
			{
				installed.Visible = true;

				Label label = (GetNode("%CurrentlyInstalledVersion") as Label);
				label.MouseFilter = MouseFilterEnum.Pass;
				label.Text = $"Currently Installed: v{installedMod.Version}";
				label.Visible = true;

				(GetNode("%InstallButton") as Button).Visible = false;
				(GetNode("%UninstallButton") as Button).Visible = true;
				if (!mod.IsLocal)
				{
					(GetNode("%DeleteButton") as Button).Visible = true;
					(GetNode("%LocalMod") as Panel).Visible = false;
					(GetNode("%UpdateModButton") as Button).Visible = ModHasUpdate(installedMod.Name);
				}
				else 
				{
					(GetNode("%DeleteButton") as Button).Visible = false;
					(GetNode("%LocalMod") as Panel).Visible = true;
				}

				if (installedMod.Disabled)
				{
					installed.SelfModulate = Godot.Color.Color8(255, 0, 0);
					(GetNode("%EnableButton") as Button).Visible = true;
				}
				else
				{
					installed.SelfModulate = Godot.Color.Color8(255, 255, 255);
					(GetNode("%DisableButton") as Button).Visible = true;
				}
			}
			else
			{
				installed.Visible = false;
				installed.SelfModulate = Godot.Color.Color8(255, 255, 255);
			}

			HBoxContainer authorsContainer = GetNode("%AuthorsContainer") as HBoxContainer;

			List<string> authors = mod.Author.Split(',', '\n').Select(p => p.Trim()).ToList();

			foreach (string author in authors)
			{
				Label label = new Label();
				label.Text = author;
				authorsContainer.AddChild(label);
			}

			VBoxContainer dependenciesContainer = GetNode("%DependenciesContainer") as VBoxContainer;

			if (mod.Requires.Count > 0)
			{
				if (mod.Requires.Count == 1 && mod.Requires[0] == string.Empty)
				{
					PackedScene dependencyPanel = ResourceLoader.Load("res://Dependency.tscn") as PackedScene;
					DependencyPanel panel = dependencyPanel.Instance() as DependencyPanel;
					panel.Init("No dependencies");

					dependenciesContainer.AddChild(panel);
				}
				else
				{
					foreach (string dep in mod.Requires)
					{
						PackedScene dependencyPanel = ResourceLoader.Load("res://Dependency.tscn") as PackedScene;
						DependencyPanel panel = dependencyPanel.Instance() as DependencyPanel;

						if (TryGetMod(dep, out Mod dependency))
						{
							panel.Init(dependency.FriendlyName);
							if (dependency.ModPanel != null && IsInstanceValid(dependency.ModPanel))
							{
								panel.Connect("gui_input", this, "OnModClicked", new Godot.Collections.Array(dependency.ModPanel));
							}
						}
						else
						{
							if (TryGetInstalledMod(dep, out Mod d))
							{
								panel.Init(d.FriendlyName);
								if (d.ModPanel != null && IsInstanceValid(d.ModPanel))
								{
									panel.Connect("gui_input", this, "OnModClicked", new Godot.Collections.Array(d.ModPanel));
								}
							} 
							else
							{
								panel.Init(dep);
							}
						}

						dependenciesContainer.AddChild(panel);
					}
				}
			} 
			else
			{
				Label label = new Label();
				label.Text = "No dependencies";

				dependenciesContainer.AddChild(label);
			}

			VBoxContainer tagsContainer = GetNode("%ModTagsContainer") as VBoxContainer;

			if (mod.Tags.Count > 0)
			{
				foreach (string tag in mod.Tags)
				{
					Label label = new Label();
					label.Text = tag;

					tagsContainer.AddChild(label);
				}
			} 
			else
			{
				Label label = new Label();
				label.Text = "No tags";

				tagsContainer.AddChild(label);
			}

			(GetNode("%Description") as Label).Text = Regex.Replace(mod.Description, "\\[\\/?[^\\]]*\\]", "");

			SelectedMod = modPanel;
		}
	}

	public void OnBundleClicked(InputEvent inputEvent, BundlePanel bundlePanel)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			if (SelectedBundle != null)
			{
				if (SelectedBundle == bundlePanel)
					return;

				//if (SelectedMod.HasStyleboxOverride("panel"))
				//	SelectedMod.RemoveStyleboxOverride("panel");
				SelectedBundle.AddStyleboxOverride("panel", DefaultModPanelStyle);
			}

			AcceptEvent();

			StyleBoxFlat style = new StyleBoxFlat();
			style.BgColor = Godot.Color.Color8(17, 19, 31);
			style.BorderColor = Godot.Color.Color8(93, 170, 243);
			style.SetBorderWidthAll(3);
			//if (SelectedMod.HasStyleboxOverride("panel"))
			//	SelectedMod.RemoveStyleboxOverride("panel");
			bundlePanel.AddStyleboxOverride("panel", style);

			ClearBundleInfo();

			Bundle bundle = bundlePanel.Bundle;
			Label bundleName = (GetNode("%BundleName") as Label);
			bundleName.Text = bundle.FriendlyName;
			bundleName.HintTooltip = bundle.FriendlyName;
			(GetNode("%BundleVersionLabel") as Label).Text = $"Version: {bundle.Version}";

			Panel installed = GetNode("%BundleInstalled") as Panel;
			if (TryGetInstalledBundle(bundle.Name, out Bundle installedBundle))
			{
				installed.Visible = true;

				Label label = (GetNode("%BundleCurrentlyInstalledVersion") as Label);
				label.MouseFilter = MouseFilterEnum.Pass;
				label.Text = $"Currently Installed: v{installedBundle.Version}";
				label.Visible = true;

				(GetNode("%BundleInstallButton") as Button).Visible = false;
				(GetNode("%BundleUninstallButton") as Button).Visible = true;
				(GetNode("%UpdateBundleButton") as Button).Visible = BundleHasUpdate(installedBundle.Name);


				if (installedBundle.Disabled)
				{
					installed.SelfModulate = Godot.Color.Color8(255, 0, 0);
					(GetNode("%BundleEnableButton") as Button).Visible = true;
				}
				else
				{
					installed.SelfModulate = Godot.Color.Color8(255, 255, 255);
					(GetNode("%BundleDisableButton") as Button).Visible = true;
				}
			}
			else
			{
				installed.Visible = false;
				installed.SelfModulate = Godot.Color.Color8(255, 255, 255);
			}

			HBoxContainer authorsContainer = GetNode("%BundleAuthorsContainer") as HBoxContainer;

			List<string> authors = bundle.Author.Split(',', '\n').Select(p => p.Trim()).ToList();

			foreach (string author in authors)
			{
				Label label = new Label();
				label.Text = author;
				authorsContainer.AddChild(label);
			}

			VBoxContainer modsContainer = GetNode("%ModsContainer") as VBoxContainer;
			foreach (string dep in bundle.Mods)
			{
				PackedScene dependencyPanel = ResourceLoader.Load("res://Dependency.tscn") as PackedScene;
				DependencyPanel panel = dependencyPanel.Instance() as DependencyPanel;

				if (TryGetMod(dep, out Mod dependency))
				{
					panel.Init(dependency.FriendlyName);
					//panel.Connect("gui_input", this, "OnModClicked", new Godot.Collections.Array(dependency.ModPanel));
				}
				else
				{
					panel.Init(dep);
				}

				modsContainer.AddChild(panel);
			}


			(GetNode("%BundleDescription") as Label).Text = Regex.Replace(bundle.Description, "\\[\\/?[^\\]]*\\]", "");

			SelectedBundle = bundlePanel;
		}
	}

	public void OnProfileClicked(InputEvent inputEvent, ModProfilePanel profilePanel)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			if (SelectedProfile != null)
			{
				if (SelectedProfile == profilePanel)
					return;

				//if (SelectedMod.HasStyleboxOverride("panel"))
				//	SelectedMod.RemoveStyleboxOverride("panel");
				SelectedProfile.AddStyleboxOverride("panel", DefaultModPanelStyle);
			}

			AcceptEvent();

			StyleBoxFlat style = new StyleBoxFlat();
			style.BgColor = Godot.Color.Color8(17, 19, 31);
			style.BorderColor = Godot.Color.Color8(93, 170, 243);
			style.SetBorderWidthAll(3);
			//if (SelectedMod.HasStyleboxOverride("panel"))
			//	SelectedMod.RemoveStyleboxOverride("panel");
			profilePanel.AddStyleboxOverride("panel", style);

			ClearProfileInfo();

			ModProfile profile = profilePanel.ModProfile;

			(GetNode("%ProfileEnableButton") as Button).Visible = profile.Disabled;
			(GetNode("%ProfileUpdateModButton") as Button).Visible = ProfileHasUpdate(profile.Name);
			(GetNode("%ProfileDisableButton") as Button).Visible = !profile.Disabled;

			Label profileName = (GetNode("%ProfileName") as Label);
			profileName.Text = profile.Name;
			profileName.HintTooltip = profile.Name;

			HBoxContainer authorsContainer = GetNode("%ProfileAuthorsContainer") as HBoxContainer;

			List<string> allAuthors = new List<string>();

			foreach (string mod in profile.Mods)
			{
				if (TryGetMod(mod, out Mod m))
				{
					allAuthors.AddRange(m.Author.Split(',', '\n').Select(p => p.Trim()).ToList());
				}
			}

			foreach (string bundle in profile.Bundles)
			{
				if (TryGetBundle(bundle, out Bundle b))
				{
					foreach (string mod in b.Mods)
					{
						if (TryGetMod(mod, out Mod m))
						{
							allAuthors.AddRange(m.Author.Split(',', '\n').Select(p => p.Trim()).ToList());
						}
					}
				}
			}

			foreach (string author in allAuthors.Distinct())
			{
				Label label = new Label();
				label.Text = author;
				authorsContainer.AddChild(label);
			}

			VBoxContainer modsContainer = GetNode("%ProfileModsContainer") as VBoxContainer;
			foreach (string dep in profile.Mods)
			{
				PackedScene dependencyPanel = ResourceLoader.Load("res://Dependency.tscn") as PackedScene;
				DependencyPanel panel = dependencyPanel.Instance() as DependencyPanel;

				if (TryGetMod(dep, out Mod dependency))
				{
					panel.Init(dependency.FriendlyName);
				}
				else
				{
					panel.Init(dep);
				}

				modsContainer.AddChild(panel);
			}

			VBoxContainer bundlesContainer = GetNode("%ProfileBundlesContainer") as VBoxContainer;
			foreach (string dep in profile.Bundles)
			{
				PackedScene dependencyPanel = ResourceLoader.Load("res://Dependency.tscn") as PackedScene;
				DependencyPanel panel = dependencyPanel.Instance() as DependencyPanel;

				if (TryGetBundle(dep, out Bundle dependency))
				{
					panel.Init(dependency.FriendlyName);
				}
				else
				{
					panel.Init(dep);
				}

				bundlesContainer.AddChild(panel);
			}

			SelectedProfile = profilePanel;
		}
	}

	public void _on_SearchBar_text_changed(string newText)
	{
		if (SelectedTab.Name == "ModProfilesTab")
		{
			SetupProfiles(newText);
		}
		else if (SelectedTab.Name == "BundlesTab" || SelectedTab.Name == "InstalledBundlesTab")
		{
			SetupBundles(newText);
		}
		else
		{
			SetupMods(newText);
		}
	}
	
	public void RecurseAndDownloadDependencies(Mod mod, List<string> didCheck, bool andEnable = true)
	{
		if (!Offline && mod.Requires.Count > 0 && !(mod.Requires.Count == 1 && mod.Requires[0] == string.Empty))
		{
			using (WebClient wc = new WebClient())
			{
				foreach (string dep in mod.Requires)
				{
					if (didCheck.Contains(dep))
						continue;
					didCheck.Add(dep);
					if (dep != string.Empty && !ModInstalled(dep) && TryGetMod(dep, out Mod depMod))
					{
						try
						{
							RecurseAndDownloadDependencies(depMod, didCheck, andEnable);
							string depPath = Path.Combine(Paths.ModsPath, $"{depMod.Name}.zip");
							wc.DownloadFile($"{Paths.RootUrl}/mod/{depMod.Name}", depPath);
							AddInstalledMod(depPath, andEnable);
						}
						catch (Exception ex)
						{
							GD.PrintErr(ex);
							_latestErrorText = ex.Message + "\n" + ex.StackTrace;
							Offline = true;
						}
					}
				}
			}
		}
	}

	public void _on_InstallButton_pressed()
	{
		if (SelectedMod != null)
		{
			bool compatible = true;

			foreach (Mod mod in InstalledMods)
			{
				if (!mod.Disabled && (SelectedMod.Mod.Incompatible.Contains(mod.Name) || mod.Incompatible.Contains(SelectedMod.Mod.Name)))
				{
					compatible = false;
					break;
				}
			}

			InstallMod(SelectedMod.Mod.Name, compatible);
		}
	}

	public void _on_UninstallButton_pressed()
	{
		if (SelectedMod != null)
		{
			UninstallMod(SelectedMod.Mod.Name);
		}
	}


	public void _on_DisableButton_pressed()
	{
		if (SelectedMod != null)
		{
			DisableMod(SelectedMod.Mod.Name);
		}
	}

	public void _on_EnableButton_pressed()
	{
		if (SelectedMod != null)
		{
			foreach (Mod mod in InstalledMods)
			{
				if (!mod.Disabled && (SelectedMod.Mod.Incompatible.Contains(mod.Name) || mod.Incompatible.Contains(SelectedMod.Mod.Name)))
				{
					ShowModIsIncompatible(SelectedMod.Mod, mod);
					return;
				}
			}
			
			EnableMod(SelectedMod.Mod.Name);
		}
	}

	public void _on_UpdateModButton_pressed()
	{
		if (SelectedMod != null)
		{
			InstallMod(SelectedMod.Mod.Name);
		}
	}

	public void _on_UninstallAnywaysButton_pressed()
	{
		if (SelectedMod != null)
		{
			UninstallMod(SelectedMod.Mod.Name, true);
			(GetNode("%ModIsDependency") as ModIsDependency).Visible = false;
		}
	}

	public void _on_DisableAnyways_pressed()
	{
		if (SelectedMod != null)
		{
			DisableMod(SelectedMod.Mod.Name, true);
			(GetNode("%ModIsDependency") as ModIsDependency).Visible = false;
		}
	}

	public void ResetUpload(bool visible = false)
	{
		(GetNode("%UploadModPanel") as Panel).Visible = visible;
		(GetNode("%ChooseModZipButton") as Button).Text = "Choose a file";
		(GetNode("%ModPassphraseEdit") as LineEdit).Text = string.Empty;
		(GetNode("%TagsBackground") as Panel).Visible = false;
		(GetNode("%TagScrollContainer") as ScrollContainer).Visible = false;

		foreach (Button button in GetNode("%TagsCheckBoxes").GetChildren())
		{
			button.Pressed = false;
		}

		ModToUploadPath = string.Empty;
		ModToUpload = null;
	}

	public void OnModClickedBundleUpload(InputEvent inputEvent, ModPanel modPanel)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			if (BundleToUpload.Mods.Contains(modPanel.Mod.Name))
			{
				BundleToUpload.Mods.Remove(modPanel.Mod.Name);
				modPanel.AddStyleboxOverride("panel", DefaultModPanelStyle);
			} 
			else
			{
				StyleBoxFlat style = new StyleBoxFlat();
				style.BgColor = Godot.Color.Color8(17, 19, 31);
				style.BorderColor = Godot.Color.Color8(93, 170, 243);
				style.SetBorderWidthAll(3);
				modPanel.AddStyleboxOverride("panel", style);

				BundleToUpload.Mods.Add(modPanel.Mod.Name);
			}
		}
	}

	public void OnModClickedProfileCreator(InputEvent inputEvent, ModPanel modPanel)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			if (ProfileToCreate.Mods.Contains(modPanel.Mod.Name))
			{
				ProfileToCreate.Mods.Remove(modPanel.Mod.Name);
				modPanel.AddStyleboxOverride("panel", DefaultModPanelStyle);
			}
			else
			{
				StyleBoxFlat style = new StyleBoxFlat();
				style.BgColor = Godot.Color.Color8(17, 19, 31);
				style.BorderColor = Godot.Color.Color8(93, 170, 243);
				style.SetBorderWidthAll(3);
				modPanel.AddStyleboxOverride("panel", style);

				ProfileToCreate.Mods.Add(modPanel.Mod.Name);
			}
		}
	}

	public void OnBundleClickedProfileCreator(InputEvent inputEvent, BundlePanel bundlePanel)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			if (ProfileToCreate.Bundles.Contains(bundlePanel.Bundle.Name))
			{
				ProfileToCreate.Bundles.Remove(bundlePanel.Bundle.Name);
				bundlePanel.AddStyleboxOverride("panel", DefaultModPanelStyle);
			}
			else
			{
				StyleBoxFlat style = new StyleBoxFlat();
				style.BgColor = Godot.Color.Color8(17, 19, 31);
				style.BorderColor = Godot.Color.Color8(93, 170, 243);
				style.SetBorderWidthAll(3);
				bundlePanel.AddStyleboxOverride("panel", style);

				ProfileToCreate.Bundles.Add(bundlePanel.Bundle.Name);
			}
		}
	}

	public void SetBundleUploadModsList(string filter = "")
	{
		VBoxContainer modsList = (GetNode("%BundleUploadModsList") as VBoxContainer);

		foreach (Node node in modsList.GetChildren())
		{
			modsList.RemoveChild(node);
			node.QueueFree();
		}

		foreach (Mod mod in Manifest.Mods)
		{
			if (filter != string.Empty)
			{
				if (!mod.FriendlyName.StartsWith(filter, StringComparison.OrdinalIgnoreCase) &&
					(filter.Length < 3 || mod.FriendlyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) == -1)
					)
				{
					continue;
				}
			}
			PackedScene modPanel = ResourceLoader.Load("res://ModPanelBundle.tscn") as PackedScene;
			ModPanelBundle panel = modPanel.Instance() as ModPanelBundle;
			panel.Connect("gui_input", this, "OnModClickedBundleUpload", new Godot.Collections.Array(panel));
			RecurseAndConnectInputBundleUpload(panel.GetChildren(), panel);
			panel.Init(mod);
			modsList.AddChild(panel);

			panel.HintTooltip = mod.FriendlyName;

			if (BundleToUpload.Mods.Contains(mod.Name))
			{
				StyleBoxFlat style = new StyleBoxFlat();
				style.BgColor = Godot.Color.Color8(17, 19, 31);
				style.BorderColor = Godot.Color.Color8(93, 170, 243);
				style.SetBorderWidthAll(3);
				panel.AddStyleboxOverride("panel", style);
			}
		}
	}

	public void SetProfileCreatorModsList(string filter = "")
	{
		VBoxContainer modsList = (GetNode("%ProfileCreatorModsList") as VBoxContainer);

		foreach (Node node in modsList.GetChildren())
		{
			modsList.RemoveChild(node);
			node.QueueFree();
		}

		foreach (Mod mod in Manifest.Mods)
		{
			if (filter != string.Empty)
			{
				if (!mod.FriendlyName.StartsWith(filter, StringComparison.OrdinalIgnoreCase) &&
					(filter.Length < 3 || mod.FriendlyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) == -1)
					)
				{
					continue;
				}
			}
			PackedScene modPanel = ResourceLoader.Load("res://ModPanelBundle.tscn") as PackedScene;
			ModPanelBundle panel = modPanel.Instance() as ModPanelBundle;
			panel.Connect("gui_input", this, "OnModClickedProfileCreator", new Godot.Collections.Array(panel));
			RecurseAndConnectInputProfileCreator(panel.GetChildren(), panel);
			panel.Init(mod);
			modsList.AddChild(panel);

			panel.HintTooltip = mod.FriendlyName;

			if (ProfileToCreate.Mods.Contains(mod.Name))
			{
				StyleBoxFlat style = new StyleBoxFlat();
				style.BgColor = Godot.Color.Color8(17, 19, 31);
				style.BorderColor = Godot.Color.Color8(93, 170, 243);
				style.SetBorderWidthAll(3);
				panel.AddStyleboxOverride("panel", style);
			}
		}
	}

	public void SetProfileCreatorBundlesList(string filter = "")
	{
		VBoxContainer bundlesList = (GetNode("%ProfileCreatorBundlesList") as VBoxContainer);

		foreach (Node node in bundlesList.GetChildren())
		{
			bundlesList.RemoveChild(node);
			node.QueueFree();
		}

		foreach (Bundle bundle in Manifest.Bundles)
		{
			if (filter != string.Empty)
			{
				if (!bundle.FriendlyName.StartsWith(filter, StringComparison.OrdinalIgnoreCase) &&
					(filter.Length < 3 || bundle.FriendlyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) == -1)
					)
				{
					continue;
				}
			}
			PackedScene modPanel = ResourceLoader.Load("res://BundlePanelProfile.tscn") as PackedScene;
			BundlePanelProfile panel = modPanel.Instance() as BundlePanelProfile;
			panel.Connect("gui_input", this, "OnBundleClickedProfileCreator", new Godot.Collections.Array(panel));
			RecurseAndConnectInputProfileCreator(panel.GetChildren(), panel);
			panel.Init(bundle);
			bundlesList.AddChild(panel);

			panel.HintTooltip = bundle.FriendlyName;

			if (ProfileToCreate.Bundles.Contains(bundle.Name))
			{
				StyleBoxFlat style = new StyleBoxFlat();
				style.BgColor = Godot.Color.Color8(17, 19, 31);
				style.BorderColor = Godot.Color.Color8(93, 170, 243);
				style.SetBorderWidthAll(3);
				panel.AddStyleboxOverride("panel", style);
			}
		}
	}

	public void ResetBundleUpload(bool visible = false)
	{
		(GetNode("%UploadBundlePanel") as Panel).Visible = visible;
		(GetNode("%BundleUploadName") as LineEdit).Text = string.Empty;
		(GetNode("%BundlePassphraseEdit") as LineEdit).Text = string.Empty;
		(GetNode("%BundleUploadSearchBar") as LineEdit).Text = string.Empty;
		(GetNode("%BundleUploadVersionEdit") as LineEdit).Text = "1.0.0";
		(GetNode("%BundleUploadDescriptionEdit") as TextEdit).Text = string.Empty;

		if (!visible)
		{
			BundleToUpload = null;

			VBoxContainer modsList = (GetNode("%BundleUploadModsList") as VBoxContainer);

			foreach (Node node in modsList.GetChildren())
			{
				modsList.RemoveChild(node);
				node.QueueFree();
			}
		}
		else
		{
			BundleToUpload = new Bundle();
			BundleToUpload.Mods = new List<string>();
			SetBundleUploadModsList();
		}
	}

	public void ResetProfileCreator(bool visible = false)
	{
		(GetNode("%CreateProfilePanel") as Panel).Visible = visible;
		(GetNode("%ProfileCreatorSearchBar") as LineEdit).Text = string.Empty;
		(GetNode("%ProfileCreatorBundleSearchBar") as LineEdit).Text = string.Empty;

		if (!visible)
		{
			ProfileToCreate = null;

			VBoxContainer modsList = (GetNode("%ProfileCreatorModsList") as VBoxContainer);

			foreach (Node node in modsList.GetChildren())
			{
				modsList.RemoveChild(node);
				node.QueueFree();
			}

			VBoxContainer bundlesList = (GetNode("%ProfileCreatorBundlesList") as VBoxContainer);

			foreach (Node node in bundlesList.GetChildren())
			{
				modsList.RemoveChild(node);
				node.QueueFree();
			}
		}
		else
		{
			ProfileToCreate = new ModProfile();
			ProfileToCreate.Mods = new List<string>();
			ProfileToCreate.Bundles = new List<string>();
			SetProfileCreatorModsList();
			SetProfileCreatorBundlesList();
		}
	}

	public async void _on_ChooseModZipButton_pressed()
	{
		FileDialog fileDialogue = GetNode("%SelectModZip") as FileDialog;
		fileDialogue.PopupCentered();

		object[] result = await ToSignal(fileDialogue, "file_selected");
		string path = (string)result[0];

		Button button = GetNode("%ChooseModZipButton") as Button;
		
		try
		{
			Mod mod = GetModFromZip(path);
			button.Text = $"Selected: {mod.FriendlyName}";
			ModToUploadPath = path;
			ModToUpload = mod;
		} 
		catch (Exception ex)
		{
			GD.PrintErr(ex);
			button.Text = "COULD NOT PARSE JSON";
		}
	}
	
	public void _on_ExitUploadButton_pressed()
	{
		ResetUpload();
	}

	public void ShowUploadMessage(string message)
	{
		DownloadManifest();
		(GetNode("%UploadMessageContainer") as Panel).Visible = true;
		(GetNode("%UploadMessage") as Label).Text = message;
	}

	public void _on_UploadModButton_pressed()
	{
		if (Offline)
			return;
		string phrase = (GetNode("%ModPassphraseEdit") as LineEdit).Text;
		if (!string.IsNullOrEmpty(ModToUploadPath) && !string.IsNullOrEmpty(phrase) && ModToUpload != null)
		{
			using (WebClient wc = new WebClient())
			{
				try
				{
					wc.Headers.Set("passphrase", phrase);
					wc.Headers.Set("name", ModToUpload.Name);

					foreach (Button button in GetNode("%TagsCheckBoxes").GetChildren())
					{
						if (button.Pressed)
						{
							GD.Print(button.Text);
							ModToUpload.Tags.Add(button.Text);
						}
					}

					wc.Headers.Set("tags", string.Join(",", ModToUpload.Tags));
					wc.UploadFile($"{Paths.RootUrl}/upload_mod", "POST", ModToUploadPath);
					ShowUploadMessage("Upload success!");
					InstallMod(ModToUpload.Name);
					ResetUpload();
					SetupMods((GetNode("%SearchBar") as LineEdit).Text);
				} 
				catch (WebException ex)
				{
					using (var reader = new StreamReader(ex.Response.GetResponseStream()))
					{
						ShowUploadMessage(reader.ReadToEnd());
					}
				}
			}
		 }
	}

	public void _on_ToggleTagSelect_pressed()
	{
		Panel background = (GetNode("%TagsBackground") as Panel);
		ScrollContainer container = (GetNode("%TagScrollContainer") as ScrollContainer);
		background.Visible = !background.Visible;
		container.Visible = !container.Visible;
	}


	public void _on_ExitUploadMessageButton_pressed()
	{
		(GetNode("%UploadMessageContainer") as Panel).Visible = false;
	}

	public void _on_UploadButton_pressed()
	{
		if (SelectedTab.Name == "ModProfilesTab")
		{
			ResetProfileCreator(true);
		}
		else if (SelectedTab.Name == "BundlesTab" || SelectedTab.Name == "InstalledBundlesTab")
		{
			ResetBundleUpload(true);
		}
		else
		{
			ResetUpload(true);
		}
		
	}

	public void ShowInfoPanel(string panelName)
	{
		Node infoPanelContainer = GetNode("%InfoPanelContainer");
		foreach (Node child in infoPanelContainer.GetChildren())
		{
			(child as Panel).Visible = child.Name == panelName;
		}

		if (panelName == "ModProfileInfoPanel")
		{
			(GetNode("%UploadButton") as Button).Text = "Create profile";
		}
		else if (panelName == "BundleInfoPanel")
		{
			(GetNode("%UploadButton") as Button).Text = "Upload bundle";
		}
		else
		{
			(GetNode("%UploadButton") as Button).Text = "Upload mod";
		}
	}

	public void _on_AllModsTab_gui_input(InputEvent inputEvent)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			(GetNode("%Title") as Label).Text = "Browse All Mods";
			Panel clicked = GetNode("%AllModsTab") as Panel;

			if (SelectedTab != null)
			{
				if (SelectedTab == clicked)
					return;
				SelectedTab.AddStyleboxOverride("panel", DefaultTabPanelStyle);
			}

			SelectedTab = clicked;

			StyleBoxFlat style = new StyleBoxFlat();
			style.BgColor = Godot.Color.Color8(29, 31, 43);
			style.BorderColor = Godot.Color.Color8(93, 170, 243);
			style.SetBorderWidthAll(3);

			clicked.AddStyleboxOverride("panel", style);

			SelectedMod = null;
			ShowInfoPanel("ModInfoPanel");
			ClearModInfo();
			(GetNode("%SearchBar") as LineEdit).Text = string.Empty;
			SetupMods();
		}
	}

	public void _on_InstalledModsTab_gui_input(InputEvent inputEvent)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			(GetNode("%Title") as Label).Text = "Browse Installed Mods";
			Panel clicked = GetNode("%InstalledModsTab") as Panel;
			
			if (SelectedTab != null)
			{
				if (SelectedTab == clicked)
					return;
				SelectedTab.AddStyleboxOverride("panel", DefaultTabPanelStyle);
			}

			SelectedTab = clicked;

			StyleBoxFlat style = new StyleBoxFlat();
			style.BgColor = Godot.Color.Color8(29, 31, 43);
			style.BorderColor = Godot.Color.Color8(93, 170, 243);
			style.SetBorderWidthAll(3);

			clicked.AddStyleboxOverride("panel", style);

			SelectedMod = null;
			SelectedBundle = null;
			ShowInfoPanel("ModInfoPanel");
			ClearModInfo();
			(GetNode("%SearchBar") as LineEdit).Text = string.Empty;
			SetupMods();
		}
	}

	public void _on_ModBundlesTab_gui_input(InputEvent inputEvent)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			(GetNode("%Title") as Label).Text = "Browse All Bundles";
			Panel clicked = GetNode("%BundlesTab") as Panel;

			if (SelectedTab != null)
			{
				if (SelectedTab == clicked)
					return;
				SelectedTab.AddStyleboxOverride("panel", DefaultTabPanelStyle);
			}

			SelectedTab = clicked;

			StyleBoxFlat style = new StyleBoxFlat();
			style.BgColor = Godot.Color.Color8(29, 31, 43);
			style.BorderColor = Godot.Color.Color8(93, 170, 243);
			style.SetBorderWidthAll(3);

			clicked.AddStyleboxOverride("panel", style);

			SelectedMod = null;
			SelectedBundle = null;
			ClearBundleInfo();
			(GetNode("%SearchBar") as LineEdit).Text = string.Empty;
			SetupBundles();
			ShowInfoPanel("BundleInfoPanel");

		}
	}

	public void _on_InstalledModBundles_gui_input(InputEvent inputEvent)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			(GetNode("%Title") as Label).Text = "Browse Installed Bundles";
			Panel clicked = GetNode("%InstalledBundlesTab") as Panel;

			if (SelectedTab != null)
			{
				if (SelectedTab == clicked)
					return;
				SelectedTab.AddStyleboxOverride("panel", DefaultTabPanelStyle);
			}

			SelectedTab = clicked;

			StyleBoxFlat style = new StyleBoxFlat();
			style.BgColor = Godot.Color.Color8(29, 31, 43);
			style.BorderColor = Godot.Color.Color8(93, 170, 243);
			style.SetBorderWidthAll(3);

			clicked.AddStyleboxOverride("panel", style);

			SelectedMod = null;
			SelectedBundle = null;
			ClearBundleInfo();
			(GetNode("%SearchBar") as LineEdit).Text = string.Empty;
			SetupBundles();
			ShowInfoPanel("BundleInfoPanel");

		}
	}

	public void _on_UploadBundleButton_pressed()
	{
		if (Offline)
			return;
		if (BundleToUpload != null || BundleToUpload.Mods.Count <= 1)
		{
			string name = (GetNode("%BundleUploadName") as LineEdit).Text;
			string passphrase = (GetNode("%BundlePassphraseEdit") as LineEdit).Text;
			string bundlePassphrase = (GetNode("%BundlePassphraseEdit2") as LineEdit).Text;
			string description = (GetNode("%BundleUploadDescriptionEdit") as TextEdit).Text;
			string version = (GetNode("%BundleUploadVersionEdit") as LineEdit).Text;
			BundleToUpload.FriendlyName = name;
			BundleToUpload.Name = string.Join("", name.ToLower().Split(' '));
			BundleToUpload.Author = string.Join(",", BundleToUpload.Mods.Select(mod =>
			{
				if (TryGetMod(mod, out Mod m))
				{
					return m.Author;
				}

				return string.Empty;
			}).Distinct());
			BundleToUpload.Description = description;
			BundleToUpload.Version = version;

			if (!string.IsNullOrEmpty(passphrase))
			{
				using (WebClient wc = new WebClient())
				{
					try
					{
						wc.Headers.Set("passphrase", passphrase);
						wc.Headers.Set("bundle_passphrase", bundlePassphrase);
						wc.Headers.Set(HttpRequestHeader.ContentType, "application/json");
						wc.UploadString($"{Paths.RootUrl}/upload_bundle", "POST", JsonConvert.SerializeObject(BundleToUpload, JsonSettings));
						ShowUploadMessage("Upload success!");
						EnableBundle(BundleToUpload.Name);
						ResetBundleUpload();
					}
					catch (WebException ex)
					{
						using (var reader = new StreamReader(ex.Response.GetResponseStream()))
						{
							ShowUploadMessage(reader.ReadToEnd());
						}
					}
				}
			}
		}
	}

	public void _on_ExitBundleUploadButton_pressed()
	{
		ResetBundleUpload();
	}

	public void _on_BundleUploadSearchBar_text_changed(string newText)
	{
		SetBundleUploadModsList(newText);
	}

	public void _on_BundleInstallButton_pressed()
	{
		if (SelectedBundle != null)
		{
			bool compatible = true;

			foreach (string m in SelectedBundle.Bundle.Mods)
			{
				if (!compatible)
					break;
				Mod mod;
				if (TryGetInstalledMod(m, out mod))
				{
					foreach (Mod incompatMod in InstalledMods)
					{
						if (!incompatMod.Disabled && (mod.Incompatible.Contains(incompatMod.Name) || incompatMod.Incompatible.Contains(mod.Name)))
						{
							compatible = false;
							break;
						}
					}
				}
				else if (TryGetMod(m, out mod))
				{
					foreach (Mod incompatMod in InstalledMods)
					{
						if (!incompatMod.Disabled && (mod.Incompatible.Contains(incompatMod.Name) || incompatMod.Incompatible.Contains(mod.Name)))
						{
							compatible = false;
							break;
						}
					}
				}
			}

			InstallBundle(SelectedBundle.Bundle.Name, compatible);
		}
	}

	public void _on_BundleDisableButton_pressed()
	{
		if (SelectedBundle != null)
		{
			DisableBundle(SelectedBundle.Bundle.Name);
		}
	}

	public void _on_UpdateBundleButton_pressed()
	{
		if (SelectedBundle != null)
		{
			InstallBundle(SelectedBundle.Bundle.Name);
		}
	}


	public void _on_BundleEnableButton_pressed()
	{
		if (SelectedBundle != null)
		{
			foreach (string m in SelectedBundle.Bundle.Mods)
			{
				Mod mod;
				if (TryGetInstalledMod(m, out mod))
				{
					foreach (Mod incompatMod in InstalledMods)
					{
						if (!incompatMod.Disabled && (mod.Incompatible.Contains(incompatMod.Name) || incompatMod.Incompatible.Contains(mod.Name)))
						{
							ShowBundleIsIncompatible();
							return;
						}
					}
				}
				else if (TryGetMod(m, out mod))
				{
					foreach (Mod incompatMod in InstalledMods)
					{
						if (!incompatMod.Disabled && (mod.Incompatible.Contains(incompatMod.Name) || incompatMod.Incompatible.Contains(mod.Name)))
						{
							ShowBundleIsIncompatible();
							return;
						}
					}
				}
			}
			
			EnableBundle(SelectedBundle.Bundle.Name);
		}
	}


	public void _on_BundleUninstallButton_pressed()
	{
		if (SelectedBundle != null)
		{
			UninstallBundle(SelectedBundle.Bundle.Name);
		}
	}

	public void _on_ModProfilesTab_gui_input(InputEvent inputEvent)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			(GetNode("%Title") as Label).Text = "Browse Mod Profiles";
			Panel clicked = GetNode("%ModProfilesTab") as Panel;

			if (SelectedTab != null)
			{
				if (SelectedTab == clicked)
					return;
				SelectedTab.AddStyleboxOverride("panel", DefaultTabPanelStyle);
			}

			SelectedTab = clicked;

			StyleBoxFlat style = new StyleBoxFlat();
			style.BgColor = Godot.Color.Color8(29, 31, 43);
			style.BorderColor = Godot.Color.Color8(93, 170, 243);
			style.SetBorderWidthAll(3);

			clicked.AddStyleboxOverride("panel", style);

			SelectedMod = null;
			SelectedBundle = null;
			ShowInfoPanel("ModProfileInfoPanel");
			ClearProfileInfo();
			(GetNode("%SearchBar") as LineEdit).Text = string.Empty;
			SetupProfiles();
		}
	}

	public void _on_ProfileCreatorSearchBar_text_changed(string newText)
	{
		SetProfileCreatorModsList(newText);
	}


	public void _on_ProfileCreatorBundleSearchBar_text_changed(string newText)
	{
		SetProfileCreatorBundlesList(newText);
	}

	public void _on_SelectCurrentlyEnabledModsProfile_pressed()
	{
		if (ProfileToCreate != null)
		{
			List<string> bundleMods = new List<string>();
			foreach (Bundle bundle in InstalledBundles)
			{
				if (!bundle.Disabled && !ProfileToCreate.Bundles.Contains(bundle.Name))
				{
					ProfileToCreate.Bundles.Add(bundle.Name);
					bundleMods.AddRange(bundle.Mods);
				}
			}

			foreach (Mod mod in InstalledMods)
			{
				if (!mod.Disabled && !bundleMods.Contains(mod.Name) && !ProfileToCreate.Mods.Contains(mod.Name))
				{
					ProfileToCreate.Mods.Add(mod.Name);
				}
			}

			SetProfileCreatorModsList((GetNode("%ProfileCreatorSearchBar") as LineEdit).Text);
			SetProfileCreatorBundlesList((GetNode("%ProfileCreatorBundleSearchBar") as LineEdit).Text);
		}
	}


	public void _on_CreateProfileButton_pressed()
	{
		if (ProfileToCreate != null)
		{
			string name = (GetNode("%ProfileCreatorProfileName") as LineEdit).Text;
			if (string.IsNullOrEmpty(name))
				return;
			ProfileToCreate.Name = name;
			ProfileToCreate.Disabled = true;

			int index = ModProfiles.FindIndex(p => p.Name == name);
			if (index != -1)
				ModProfiles.RemoveAt(index);
			ModProfiles.Add(ProfileToCreate);
			File.WriteAllText(Paths.ModProfilesPath, JsonConvert.SerializeObject(ModProfiles, JsonSettings));
			SetupProfiles();
		}

		ResetProfileCreator();
	}


	public void _on_ExitProfileCreator_pressed()
	{
		ResetProfileCreator();
	}

	public async void _on_ImportProfileButton_pressed()
	{
		// Replace with function body.
		FileDialog fileDialog = (GetNode("%SelectProfileFile") as FileDialog);
		fileDialog.PopupCentered();

		object[] result = await ToSignal(fileDialog, "file_selected");
		string path = (string)result[0];

		if (File.Exists(path))
		{
			string json = File.ReadAllText(path);
			try
			{
				ModProfile modProfile = JsonConvert.DeserializeObject<ModProfile>(json, JsonSettings);

				if (modProfile.Name == string.Empty)
					return;

				if (modProfile.Mods == null)
					modProfile.Mods = new List<string>();

				if (modProfile.Bundles == null)
					modProfile.Bundles = new List<string>();

				int index = ModProfiles.FindIndex(p => p.Name == modProfile.Name);
				if (index != -1)
					ModProfiles.RemoveAt(index);
				ModProfiles.Add(modProfile);
				File.WriteAllText(Paths.ModProfilesPath, JsonConvert.SerializeObject(ModProfiles, JsonSettings));
				ResetProfileCreator();
				SetupProfiles();
			} 
			catch(Exception) { }
		}
	}


	public async void _on_ProfileExportButton_pressed()
	{
		if (SelectedProfile != null)
		{
			FileDialog fileDialog = (GetNode("%SelectProfileExport") as FileDialog);
			fileDialog.PopupCentered();

			object[] result = await ToSignal(fileDialog, "dir_selected");
			string path = (string)result[0];

			File.WriteAllText(Path.Combine(path, $"{SelectedProfile.ModProfile.Name}.json"), JsonConvert.SerializeObject(SelectedProfile.ModProfile, JsonSettings));
		}
	}


	public void _on_ProfileDisableButton_pressed()
	{
		if (SelectedProfile != null)
		{
			DisableProfile(SelectedProfile.ModProfile.Name);
		}
	}


	public void _on_ProfileEnableButton_pressed()
	{
		if (SelectedProfile != null)
		{
			EnableProfile(SelectedProfile.ModProfile.Name);
		}
	}


	public void _on_ProfileDeleteButton_pressed()
	{
		if (SelectedProfile != null)
		{
			DeleteProfile(SelectedProfile.ModProfile.Name);
		}
	}


	public void _on_ProfileUpdateModButton_pressed()
	{
		if (SelectedProfile != null)
		{
			UpdateProfile(SelectedProfile.ModProfile.Name);
		}
	}

	public void _on_HelpButton_gui_input(InputEvent inputEvent)
	{
		if (inputEvent == null || (inputEvent is InputEventMouseButton mouseInput && mouseInput.Pressed && mouseInput.ButtonIndex == 1))
		{
			(GetNode("%SelectYomiInstallLocation") as Button).Visible = true;
			(GetNode("%HelpPopup") as Popup).Show();
		}
	}
	
	public void UpdateClient()
	{
		string workingPath = AppDomain.CurrentDomain.BaseDirectory;
		string tmpPath = Path.Combine(workingPath, "tmp");
		string tmpUpdaterPath = Path.Combine(tmpPath, "ModManagerUpdater.exe");
		string updaterPath = Path.Combine(workingPath, "ModManagerUpdater.exe");

		if (!Directory.Exists(tmpPath))
			Directory.CreateDirectory(tmpPath);

		using (WebClient wc = new WebClient())
		{
			wc.DownloadFile($"{Paths.RootUrl}/updater", tmpUpdaterPath);
		}

		if (File.Exists(updaterPath))
			File.Delete(updaterPath);

		File.Move(tmpUpdaterPath, updaterPath);

		Directory.Delete(tmpPath, true);

		Process.Start(updaterPath);
		Environment.Exit(0);
	}

	public void _on_UpdateClientButton_pressed()
	{
		UpdateClient();
	}

	private async void _on_SelectYomiInstallLocation_pressed()
	{
		//do
		//{
		await ToSignal(GetTree(), "idle_frame");
		FileDialog fileDialog = GetNode("%SelectYomiLocation") as FileDialog;
		fileDialog.PopupCentered();

		object[] result = await ToSignal(fileDialog, "file_selected");
		string path = Path.GetDirectoryName((string)result[0]);
		GD.Print("Selected: " + path);

		Config.YomiInstallLocation = path;
		//} 
		//while (Config.YomiInstallLocation == string.Empty || !Directory.Exists(Paths.YomiModsPath));

		File.WriteAllText(Paths.ConfigPath, JsonConvert.SerializeObject(Config, JsonSettings));
	}

	public void _on_DeleteButton_pressed()
	{
		(GetNode("%DeleteModDialog") as Panel).Visible = true;
	}


	public void _on_ExitDeleteModDialog_pressed()
	{
		(GetNode("%DeleteModDialog") as Panel).Visible = false;
		(GetNode("%DeleteModPassphrase") as LineEdit).Text = string.Empty;
	}


	public void _on_DeleteModButton_pressed()
	{
		if (SelectedMod != null)
		{
			(GetNode("%DeleteModDialog") as Panel).Visible = false;

			LineEdit pass = (GetNode("%DeleteModPassphrase") as LineEdit);

			string passphrase = pass.Text;
			pass.Text = string.Empty;

			using (WebClient wc = new WebClient())
			{
				try
				{
					wc.Headers.Set(HttpRequestHeader.ContentType, "application/json");
					wc.UploadString($"{Paths.RootUrl}/delete_mod", "POST", $"{{\"mod\":\"{SelectedMod.Mod.Name}\", \"passphrase\":\"{passphrase}\"}}");
					ShowUploadMessage("Mod deleted!");
				}
				catch (WebException ex)
				{
					using (var reader = new StreamReader(ex.Response.GetResponseStream()))
					{
						ShowUploadMessage(reader.ReadToEnd());
					}
				}
			}
		}
	}

	private void _on_BundleDeleteButton_pressed()
	{
		(GetNode("%DeleteBundleDialog") as Panel).Visible = true;
	}

	private void _on_ExitDeleteBundleDialog_pressed()
	{
		(GetNode("%DeleteBundleDialog") as Panel).Visible = false;
		(GetNode("%DeleteBundlePassphrase") as LineEdit).Text = string.Empty;
	}


	private void _on_DeleteBundleButton_pressed()
	{
		if (SelectedBundle != null)
		{
			(GetNode("%DeleteBundleDialog") as Panel).Visible = false;

			LineEdit pass = (GetNode("%DeleteBundlePassphrase") as LineEdit);

			string passphrase = pass.Text;
			pass.Text = string.Empty;

			using (WebClient wc = new WebClient())
			{
				try
				{
					wc.Headers.Set(HttpRequestHeader.ContentType, "application/json");
					wc.UploadString($"{Paths.RootUrl}/delete_bundle", "POST", $"{{\"bundle\":\"{SelectedBundle.Bundle.Name}\", \"passphrase\":\"{passphrase}\"}}");
					ShowUploadMessage("Bundle deleted!");
				}
				catch (WebException ex)
				{
					using (var reader = new StreamReader(ex.Response.GetResponseStream()))
					{
						ShowUploadMessage(reader.ReadToEnd());
					}
				}
			}
		}
	}

	public void ShowModIsIncompatible(Mod mod1, Mod mod2)
	{
		(GetNode("%ModIsIncompatible") as Panel).Visible = true;
		(GetNode("%ModIsIncompatibleLabel") as Label).Text = $"{mod1.FriendlyName} is incompatible with {mod2.FriendlyName}. Enabling them together may cause problems.";
		(GetNode("%EnableAnyways") as Button).Visible = true;
		(GetNode("%EnableAnywaysBundle") as Button).Visible = false;
	}

	public void ShowBundleIsIncompatible()
	{
		(GetNode("%ModIsIncompatible") as Panel).Visible = true;
		(GetNode("%ModIsIncompatibleLabel") as Label).Text = $"{SelectedBundle.Bundle.FriendlyName} contains mods that are incompatible with currently enabled mods. Enabling it may cause problems.";
		(GetNode("%EnableAnyways") as Button).Visible = false;
		(GetNode("%EnableAnywaysBundle") as Button).Visible = true;
	}

	public void _on_CloseIncompatibleButton_pressed()
	{
		(GetNode("%ModIsIncompatible") as Panel).Visible = false;
	}


	public void _on_EnableAnyways_pressed()
	{
		(GetNode("%ModIsIncompatible") as Panel).Visible = false;
		EnableMod(SelectedMod.Mod.Name);
	}

	public void _on_EnableAnywaysBundle_pressed()
	{
		(GetNode("%ModIsIncompatible") as Panel).Visible = false;
		EnableBundle(SelectedBundle.Bundle.Name);
	}

	public void _on_AutoUpdateClient_toggled(bool pressed)
	{
		Config.AutoUpdateClient = pressed;
		File.WriteAllText(Paths.ConfigPath, JsonConvert.SerializeObject(Config, JsonSettings));
	}

	public void _on_AutoUpdateMods_toggled(bool pressed)
	{
		Config.AutoUpdateMods = pressed;
		File.WriteAllText(Paths.ConfigPath, JsonConvert.SerializeObject(Config, JsonSettings));
	}

	public void _on_OpenSettings_pressed()
	{
		(GetNode("%SettingsMenu") as Popup).PopupCentered();
		(GetNode("%AutoUpdateClient") as CheckButton).Pressed = Config.AutoUpdateClient;
		(GetNode("%AutoUpdateMods") as CheckButton).Pressed = Config.AutoUpdateMods;
	}

	public void _on_SearchTagFilter_pressed()
	{
		Panel filterPanel = (GetNode("%TagsFilterContainer") as Panel);
		filterPanel.Visible = !filterPanel.Visible;
	}

	public void _on_ErrorMessageButton_pressed()
	{
		(GetNode("%ErrorMessageContainer") as Panel).Visible = false;
	}

	public void _on_CopyErrorButton_pressed()
	{
		OS.Clipboard = _latestErrorText;
	}

}
