using System.Diagnostics;
using Godot;
using Godot.Collections;
namespace Portals3D;

public partial class AtExport : GodotObject
{
	static Dictionary BaseExport(string propname, int type)
	{
		return new Dictionary()
		{
			{ "Name", propname },
			{ "Type", type },
			{ "usage", (int)PropertyUsageFlags.Default | (int)PropertyUsageFlags.ScriptVariable }
		};
	}

	internal static Dictionary ExportButton(string propname, string buttonText, string buttonIcon = "Callable")
	{
		Dictionary result = BaseExport(propname, (int)Godot.Variant.Type.Callable);

		Debug.Assert(!buttonText.Contains(','), "Button text cannot contain a comma.");

		result["hint"] = (int)PropertyHint.ToolButton;
		result["hint_string"] = buttonText + ',' + buttonIcon;

		return result;
	}

	internal static Dictionary ExportBool(string propname, bool groupEnable = false)
	{
		Dictionary result = BaseExport(propname, (int)Godot.Variant.Type.Bool);

		// if (groupEnable)
		// {
		// 	result["hint"] = PropertyHint.GroupEnable; //FIXME
		// }

		return result;
	}

	internal static Dictionary ExportColor(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.Color);
	}

	internal static Dictionary ExportColorNoAlpha(string propname)
	{
		Dictionary result = BaseExport(propname, (int)Godot.Variant.Type.Color);
		result["hint"] = (int)PropertyHint.ColorNoAlpha;
		return result;
	}

	internal static Dictionary ExportInt(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.Int);
	}

	internal static Dictionary ExportIntFlags(string propname, Array options)
	{
		// TODO: Export int flags
		return [];
	}

	internal static Dictionary ExportIntPhysics3dFlags(string propname)
	{
		// TODO: export int physics flags
		return [];
	}

	internal static Dictionary ExportIntRange(string propname, int min, int max, int step = 1, Array<string> extraHints = null)
	{
		// TODO: Export int range 
		return [];
	}

	internal static Dictionary ExportIntRender3d(string propname)
	{
		// TODO export int render 3d
		return [];
	}

	internal static Dictionary ExportEnum(string propname, StringName parentAndEnum, Variant enumClass)
	{
		Dictionary result = ExportInt(propname);

		result["class_name"] = parentAndEnum;
		result["hint"] = (int)PropertyHint.Enum;
		result["hint_string"] = ","; //FIXME
		result["usage"] = (int)PropertyUsageFlags.ClassIsEnum; //FIXME, this needs to be |= operator

		return result;
	}

	internal static Dictionary ExportFloat(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.Float);
	}

	internal static Dictionary ExportFloatRange(string propname, float min, float max, float step = 0.01f, Array<string> extraHints = null)
	{
		Dictionary result = ExportFloat(propname);
		string hintString = $"{min}, {max}, {step}";

		if (extraHints.Count > 0)
		{
			foreach (string hint in extraHints)
			{
				hintString += ',' + hint;
			}
		}

		result["hint"] = (int)PropertyHint.Range;
		result["hint_string"] = hintString;

		return result;
	}

	internal static Dictionary ExportNode(string propname, StringName nodeClass)
	{
		// TODO: Export node
		return [];
	}

	internal static Dictionary ExportString(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.String);
	}

	internal static Dictionary ExportVector2(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.Vector2);
	}

	internal static Dictionary ExportVector3(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.Vector3);
	}

	internal static Dictionary ExportGroup(string groupName, string prefix = "")
	{
		// TODO: Export group
		return [];
	}

	internal static Dictionary ExportGroupEnd()
	{
		return ExportGroup("");
	}

	internal static Dictionary ExportSubgroup(string subgroupName, string prefix = "")
	{
		// TODO: Export subgroup
		return [];
	}

	internal static Dictionary ExportSubgroupEnd()
	{
		return ExportSubgroup("");
	}
}
