using Godot;
using Godot.Collections;
namespace Portals3D;

public partial class PortalSettings : GodotObject
{
	static string QualName(string setting)
	{
		return "addons/portals/" + setting;
	}

	static void InitSetting(string setting, Variant defaultValue, bool requiresRestart = false)
	{
		setting = QualName(setting);

		if (!ProjectSettings.HasSetting(setting))
		{
			ProjectSettings.SetSetting(setting, defaultValue);
		}

		ProjectSettings.SetInitialValue(setting, defaultValue);
		ProjectSettings.SetRestartIfChanged(setting, requiresRestart);
		ProjectSettings.SetAsBasic(setting, true);
	}

	static void AddInfo(Dictionary config)
	{
		string qualName = QualName((string)config["name"]);

		config["name"] = qualName;

		config.Remove("usage");

		ProjectSettings.AddPropertyInfo(config);
	}

	static Variant GetSetting(string setting)
	{
		setting = QualName(setting);
		return ProjectSettings.GetSetting(setting);
	}
}
