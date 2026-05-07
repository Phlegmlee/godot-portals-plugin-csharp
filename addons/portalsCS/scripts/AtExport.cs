using System;
using System.Diagnostics;
using Godot;
using Godot.Collections;
namespace Portals3D;

public partial class AtExport : GodotObject
{
	static Dictionary<string, Variant> BaseExport(string propname, int type)
	{
		return new Dictionary<string, Variant>()
		{
			{ "Name", propname },
			{ "Type", type },
			{ "usage", (int)PropertyUsageFlags.Default | (int)PropertyUsageFlags.ScriptVariable }
		};
	}

	static Dictionary<string, Variant> ExportButton(string propname, string buttonText, string buttonIcon = "Callable")
	{
		Dictionary<string, Variant> result = BaseExport(propname, (int)Godot.Variant.Type.Callable);

		Debug.Assert(!buttonText.Contains(","), "Button text cannot contain a comma.");

		result["hint"] = (int)PropertyHint.ToolButton;
		result["hint_string"] = buttonText + "," + buttonIcon;

		return result;
	}

	static Dictionary<string, Variant> ExportBool(string propname, bool groupEnable = false)
	{
		Dictionary<string, Variant> result = BaseExport(propname, (int)Godot.Variant.Type.Bool);

		if (groupEnable)
		{
			result["hint"] = PropertyHint.GroupEnable; //FIXME
		}

		return result;
	}
}
