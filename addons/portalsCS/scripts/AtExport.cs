using System.Diagnostics;
using System.Linq;
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

		Debug.Assert(!buttonText.Contains(','), "Button text cannot contain a comma.");

		result["hint"] = (int)PropertyHint.ToolButton;
		result["hint_string"] = buttonText + ',' + buttonIcon;

		return result;
	}

	static Dictionary<string, Variant> ExportBool(string propname, bool groupEnable = false)
	{
		Dictionary<string, Variant> result = BaseExport(propname, (int)Godot.Variant.Type.Bool);

		// if (groupEnable)
		// {
		// 	result["hint"] = PropertyHint.GroupEnable; //FIXME
		// }

		return result;
	}

	static Dictionary<string, Variant> ExportColor(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.Color);
	}

	static Dictionary<string, Variant> ExportColorNoAlpha(string propname)
	{
		Dictionary<string, Variant> result = BaseExport(propname, (int)Godot.Variant.Type.Color);
		result["hint"] = (int)PropertyHint.ColorNoAlpha;
		return result;
	}

	static Dictionary<string, Variant> ExportInt(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.Int);
	}

	static Dictionary<string, Variant> ExportIntFlags(string propname, Array options)
	{
		// TODO
	}

	static Dictionary<string, Variant> ExportIntPhysics3dFlags(string propname)
	{
		// TODO
	}

	static Dictionary<string, Variant> ExportIntRange(string propname, int min, int max, int step = 1, Array<string> extraHints = null)
	{
		// TODO
	}

	static Dictionary<string, Variant> ExportIntRender3d(string propname)
	{
		// TODO
	}

	static Dictionary<string, Variant> ExportEnum(string propname, StringName parentAndEnum, Variant enumClass)
	{
		Dictionary<string, Variant> result = ExportInt(propname);

		result["class_name"] = parentAndEnum;
		result["hint"] = (int)PropertyHint.Enum;
		result["hint_string"] = ","; //FIXME
		result["usage"] = (int)PropertyUsageFlags.ClassIsEnum; //FIXME, this needs to be |= operator

		return result;
	}

	static Dictionary<string, Variant> ExportFloat(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.Float);
	}

	static Dictionary<string, Variant> ExportFloatRange(string propname, float min, float max, float step = 0.01f, Array<string> extraHints = null)
	{
		Dictionary<string, Variant> result = ExportFloat(propname);
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

	static Dictionary<string, Variant> ExportNode(string propname, StringName nodeClass)
	{
		// TODO
	}

	static Dictionary<string, Variant> ExportString(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.String);
	}

	static Dictionary<string, Variant> ExportVector2(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.Vector2);
	}

	static Dictionary<string, Variant> ExportVector3(string propname)
	{
		return BaseExport(propname, (int)Godot.Variant.Type.Vector3);
	}

	static Dictionary<string, Variant> ExportGroup(string groupName, string prefix = "")
	{
		// TODO
	}

	static Dictionary<string, Variant> ExportGroupEnd()
	{
		return ExportGroup("");
	}

	static Dictionary<string, Variant> ExportSubgroup(string subgroupName, string prefix = "")
	{
		// TODO
	}

	static Dictionary<string, Variant> ExportSubgroupEnd()
	{
		return ExportSubgroup("");
	}
}
