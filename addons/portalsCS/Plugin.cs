#if TOOLS
using Godot;
namespace Portals3D;

[Tool]
public partial class Plugin : EditorPlugin
{
	public override void _EnterTree()
	{
		PortalSettings.InitSetting("PortalsGroupName", "Portals");
		PortalSettings.AddInfo(AtExport.ExportString("PortalsGroupName"));
	}

	public override void _ExitTree()
	{
	}
}
#endif
