#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
namespace Portal3D;

/*
	Seamless 3D portal

	To get started, create two Portal3D instances and set their [member exit_portal] to each other.
	This creates a linked portal pair that you can look through. Make your player to collide with
	[member teleport_collision_mask] and you will be able to walk back and forth through the portal.
	[br][br]
	To integrate portals into your game, you can make use of the [signal on_teleport] and 
	[signal on_teleport_receive] signals. You can link a portal a different one by chaning its 
	[member exit_portal] during gameplay. The next level is to make use of the portal's callbacks,
	mainly the [member ON_TELEPORT_CALLBACK]. If you need to raycast through a portal, then the 
	[method forward_raycast] method might come in handy! When it comes to optimization, you can use
	the [method Activate] and [method Deactivate] methods to control which portals are consuming 
	resources.
	[br][br]
	[b]TIP:[/b] If you change the default value of some property, it will not get synchronized into existing 
	portal instances due to how Godot handles custom inspectors. For easier defaults management, 
	I recommend creating a scene with Portal3D as a root and re-using that.
*/

[Tool]
public partial class Portal3D : Node3D
{
	#region Public API

	// Emitted when this portal teleports something. Also see signal on_teleport_receive.
	[Signal] public delegate void OnTeleportEventHandler(Node3D node);

	// Emitted when this portal receives a teleported node.
	// Whoever had this portal as its member exit_portal triggered a teleport!
	[Signal] public delegate void OnTeleportReceiveEventHandler(Node3D node);

	public void Activate()
	{
		ProcessMode = Node.ProcessModeEnum.Inherit;

		if (PortalViewport == null)
		{
			SetupCameras();
		}

		Show();
	}

	public void Deactivate(bool destroyViewports = false)
	{
		Hide();

		WatchlistTeleportables.Clear();

		if (destroyViewports)
		{
			if (PortalViewport)
			{
				PortalViewport.QueueFree();
				PortalViewport = null;
				PortalCamera = null;
			}
		}

		ProcessMode = Node.ProcessModeEnum.Disabled;
	}

	public Dictionary ForwardRaycast(RayCast3D rayCast)
	{
		Vector3 start = ToExitPosition(rayCast.GetCollisionPoint());
		Vector3 goal = ToExitPosition(rayCast.ToGlobal(rayCast.TargetPosition));

		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create
		(
			start,
			goal,
			rayCast.CollisionMask,
			[] //TODO: Finish parameters
		);
		query.CollideWithAreas = rayCast.CollideWithAreas;
		query.CollideWithBodies = rayCast.CollideWithBodies;
		query.HitBackFaces = rayCast.HitBackFaces;
		query.HitFromInside = rayCast.HitFromInside;

		return GetWorld3D().DirectSpaceState.IntersectRay(query);
	}

	public Dictionary ForwardRaycastQuery(PhysicsRayQueryParameters3D parameters)
	{
		Vector3 start = ToExitPosition(parameters.From);
		Vector3 end = ToExitPosition(parameters.To);
		start = ExitPortal.LineIntersection(start, end);

		Array excludes = [this.TeleportArea, ExitPortal.TeleportArea];
		excludes.Append(parameters.Exclude);

		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create
		(
			start,
			end,
			parameters.CollisionMask,
			excludes
		);
		query.CollideWithAreas = parameters.CollideWithAreas;
		query.CollideWithBodies = parameters.CollideWithBodies;
		query.HitBackFaces = parameters.HitBackFaces;
		query.HitFromInside = parameters.HitFromInside;

		return GetWorld3D().DirectSpaceState.IntersectRay(query);
	}

	#endregion

	private Vector2 _portalSize = new(2.0f, 2.5f);
	public Vector2 PortalSize
	{
		get => _portalSize;
		set
		{
			_portalSize = value;
			if (CausedByUserInteraction())
			{
				OnPortalSizeChanged();
				UpdateConfigurationWarnings();
				ExitPortal?.UpdateConfigurationWarnings();
			}
		} 
	}

	private Portal3D _exitPortal = null;
	public Portal3D ExitPortal
	{
		get => _exitPortal;
		set
		{
			_exitPortal = value;
			UpdateConfigurationWarnings();
			NotifyPropertyListChanged();
		}
	}

	// TODO: Pair Portal and Sync Portal Editor Buttons here

	public Camera3D PlayerCamera;

	public float PortalFrameWidth = 0.0f;

	public enum PortalViewportSizeMode
	{
		Full,
		MaxWidthAbsolute,
		Fractional
	}
	private PortalViewportSizeMode _viewportSizeMode = PortalViewportSizeMode.Full;
	public PortalViewportSizeMode ViewportSizeMode
	{
		get => _viewportSizeMode;
		set
		{
			_viewportSizeMode = value;
			NotifyPropertyListChanged();
		}
	}

	public int ViewportSizeMaxWidthAbsolute = (int)ProjectSettings.GetSetting("display/window/size/viewport_width");
	public float ViewportSizeFractional = 0.5f;

	public enum PortalViewDirection
	{
		FrontAndBack,
		OnlyFront,
		OnlyBack
	}
	private PortalViewDirection _viewDirection = PortalViewDirection.FrontAndBack;
	public PortalViewDirection ViewDirection
	{
		get => _viewDirection;
		set => _viewDirection = value;
	}

	private int _portalRenderLayer = 1 << 19;
	public int PortalRenderLayer
	{
		get => _portalRenderLayer;
		set
		{
			_portalRenderLayer = value;
			if (CausedByUserInteraction())
			{
				PortalMesh.Layers = value;
			}
		}
	}

	// TODO: CONTINUE HERE with is_teleport on line 239

	#region Internal

	#endregion
}
#endif