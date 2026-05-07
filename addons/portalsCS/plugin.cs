#if TOOLS
using Godot;
using Godot.Collections;
namespace Portals3D;

[Tool]
public partial class Plugin : EditorPlugin
{
	public override void _EnterTree()
	{
		PortalSettings.InitSetting("PortalsGroupName", "Portals");
		PortalSettings.AddInfo((Dictionary)AtExport.ExportString("PortalsGroupName"));
	}

	public override void _ExitTree()
	{
	}
}
#endif
