#if TOOLS
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using Godot.Collections;
namespace Portals3D;

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

[Tool, GlobalClass]
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
			if (PortalViewport != null)
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

		Array<Rid> excludes = [this.TeleportArea.GetRid(), ExitPortal.TeleportArea.GetRid()];
		excludes.AddRange(parameters.Exclude);

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

	//const StringName OnTeleportCallback = &"OnTeleport";
	// TODO: const callbacks

	#endregion

	#region Export Members

	private Vector2 _portalSize = new(2.0f, 2.5f);
	[Export] public Vector2 PortalSize
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
	[Export] public Portal3D ExitPortal
	{
		get => _exitPortal;
		set
		{
			_exitPortal = value;
			UpdateConfigurationWarnings();
			NotifyPropertyListChanged();
		}
	}

	// FIXME these may need to be public???
	internal Callable _TbPairPortals = Callable.From(() => EditorPairPortals());
	internal Callable _TbSyncPortalSizes = Callable.From(() => EditorSyncPortalSizes());

	[ExportGroup("Rendering")]
	[Export] public Camera3D PlayerCamera;

	private float _portalFrameWidth = 0.0f;
	[Export(PropertyHint.Range, "0.0, 10.0, 0.01")] public float PortalFrameWidth
	{
		get => _portalFrameWidth;
		set => _portalFrameWidth = value;
	}

	public enum PortalViewportSizeMode
	{
		Full,
		MaxWidthAbsolute,
		Fractional
	}
	private PortalViewportSizeMode _viewportSizeMode = PortalViewportSizeMode.Full;
	[Export] public PortalViewportSizeMode ViewportSizeMode
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
	[Export] public PortalViewDirection ViewDirection
	{
		get => _viewDirection;
		set => _viewDirection = value;
	}

	private uint _portalRenderLayer = 1 << 19;
	[Export(PropertyHint.Layers3DRender)] public uint PortalRenderLayer
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

	private bool _isTeleport = true;
	[ExportGroup("Teleport")]
	[Export] public bool IsTeleport
	{
		get => _isTeleport;
		set
		{
			_isTeleport = value;
			if (CausedByUserInteraction())
			{
				SetupTeleport();
				NotifyPropertyListChanged();
			}
		}
	}

	public enum PortalTeleportDirection
	{
		Front,
		Back,
		FrontAndBack
	}
	private PortalTeleportDirection _teleportDirection = PortalTeleportDirection.FrontAndBack;
	[Export] public PortalTeleportDirection TeleportDirection
	{
		get => _teleportDirection;
		set => _teleportDirection = value;
	}

	private float _rigidbodyBoost = 0.0f;
	[Export(PropertyHint.Range, "0.0, 5.0, 0.01, or_greater")] public float RigidbodyBoost
	{
		get => _rigidbodyBoost;
		set => _rigidbodyBoost = value;
	}

	private float _teleportTolerance = 0.5f;
	[Export(PropertyHint.Range, "0.0, 5.0, 0.01, or_greater")] public float TeleportTolerance
	{
		get => _teleportTolerance;
		set => _teleportTolerance = value;
	}

	public enum PortalTeleportInteractions
	{
		Callback = 1 << 0,
		PlayerUpright = 1 << 1,
		DuplicateMeshes = 1 << 2
	}
	private int _teleportInteractions = (int)PortalTeleportInteractions.Callback | (int)PortalTeleportInteractions.PlayerUpright;
	[Export(PropertyHint.Flags, "Callback, Player Upright, Duplicate Meshes")] public int TeleportInteractions
	{
		get => _teleportInteractions;
		set => _teleportInteractions = value;
	}

	private int _teleportCollisionMask = 1 << 15;
	[Export(PropertyHint.Layers3DPhysics)] public int TeleportCollisionMask
	{
		get => _teleportCollisionMask;
		set => _teleportCollisionMask = value;
	}

	private bool _startDeactivated = false;
	[ExportGroup("Advanced")]
	[Export] public bool StartDeactivated
	{
		get => _startDeactivated;
		set => _startDeactivated = value;
	}

	#endregion

	#region Internal

	//FIXME: These first 4 members are @export_storage in gdscript, this is what I chose to do for the rewrite.
	private float _portalThickness = 0.05f;
	internal float PortalThickness
	{
		get => _portalThickness;
		set
		{
			_portalThickness = value;
			if (CausedByUserInteraction()) OnPortalSizeChanged();
		}
	}

	private NodePath _portalMeshPath;
	internal NodePath PortalMeshPath
	{
		get => _portalMeshPath;
		set => _portalMeshPath = value;
	}
	internal MeshInstance3D PortalMesh
	{
		get
		{
			return PortalMeshPath != null ? GetNode<MeshInstance3D>(PortalMeshPath) : null;
		}
		set
		{
			Debug.Assert(false, "Proxy variable, use 'PortalMeshPath' instead.");
		}
	}

	private NodePath _teleportAreaPath;
	internal NodePath TeleportAreaPath
	{
		get => _teleportAreaPath;
		set => _teleportAreaPath = value;
	}
	internal Area3D TeleportArea
	{
		get
		{
			return TeleportAreaPath != null ? GetNode<Area3D>(TeleportAreaPath) : null;
		}
		set
		{
			Debug.Assert(false, "Proxy variable, use 'TeleportAreaPath' instead.");
		}
	}

	private NodePath _teleportColliderPath;
	internal NodePath TeleportColliderPath
	{
		get => _teleportColliderPath;
		set => _teleportColliderPath = value;
	}
	internal CollisionShape3D TeleportCollider
	{
		get
		{
			return TeleportColliderPath != null ? GetNode<CollisionShape3D>(TeleportColliderPath) : null;
		}
		set
		{
			Debug.Assert(false, "Proxy variable, use 'TeleportColliderPath' instead.");
		}
	}

	internal Camera3D PortalCamera = null;

	internal SubViewport PortalViewport = null;

	internal partial class TeleportableMetadata : GodotObject
	{
		public float Forward = 0.0f;
		public bool IsPlayer = false;
		public Array<MeshInstance3D> Meshes = [];
		public Array<MeshInstance3D> MeshClones = [];
	}

	internal Godot.Collections.Dictionary<int, TeleportableMetadata> WatchlistTeleportables = [];

	#endregion

	#region Editor Configuration

	// TODO: Portal shader here
	// TODO: Editor preview material here

	private void _EditorReady()
	{
		//AddToGroup()
		SetNotifyTransform(true);

		ProcessPriority = 100;
		ProcessPhysicsPriority = 100;

		SetupMesh();
		SetupTeleport();

		this.GroupNode(this);
	}

	private void Notification(long what)
	{
		switch (what)
		{
			case NotificationTransformChanged:
				UpdateGizmos();
				break;

			default:
				break;
		}
	}

	private static void EditorPairPortals()
	{
		// TODO: Editor Pair Portals
	}

	private static void EditorSyncPortalSizes()
	{
		// TODO: Editor Sync Portal Size
	}

	private void SetupTeleport()
	{
		if (!IsTeleport)
		{
			if (TeleportArea != null)
			{
				TeleportArea.QueueFree();
				TeleportAreaPath = new NodePath("");
			}
			if (TeleportCollider != null)
			{
				TeleportCollider.QueueFree();
				TeleportColliderPath = new NodePath("");
			}
			return;
		}

		if (TeleportArea != null && TeleportCollider != null) return;

		Area3D area = new() { Name = "TeleportArea" };

		// TODO: Method AddChildInEditor

		TeleportAreaPath = GetPathTo(area);

		CollisionShape3D collider = new() { Name = "TeleportCollider" };
		BoxShape3D box = new();
		box.Size = box.Size with { X = PortalSize.X, Y = PortalSize.Y };
		collider.Shape = box;

		// TODO: Method AddChildInEditor
		TeleportColliderPath = GetPathTo(collider);
	}

	private void OnPortalSizeChanged()
	{
		if (PortalMesh == null) return;

		// TODO: Implement PortalBoxMesh 
		// Finish this method
	}

	#endregion

	#region Gameplay Logic

	private void SetupMesh()
	{
		// TODO: Setup Mesh Method
	}

	private void SetupCameras()
	{
		// TODO: Setup Cameras Method
	}

	#endregion

	#region Event Handlers

	#endregion

	#region UTILS

	private void ContructTpMetadata(Node3D node)
	{
		// TODO: Construct TP Metadata
	}

	private void EraseTpMetadata(int nodeId)
	{
		// TODO: Erase TP Metadata
	}

	private void TransferTpMetadataToExit(Node3D body)
	{
		// TODO: Transfer TP Metadata
	}

	private void EnableMeshClipping(TeleportableMetadata metadata, Portal3D alongPortal)
	{
		// TODO: Enable Mesh Clipping
	}

	private void DisableMeshClipping(MeshInstance3D meshInstance)
	{
		// TODO: Disable mesh clipping
	}

	private Transform3D ToExitTransform(Transform3D gTransform)
	{
		// TODO: To Exit transform
		return new Transform3D();
	}

	private Vector3 ToExitDirection(Vector3 real)
	{
		// TODO: To Exit Direction
		return Vector3.Zero;
	}

	private Vector3 ToExitPosition(Vector3 gPos)
	{
		// TODO: To Exit Position method
		return Vector3.Zero;
	}

	private float ForwardDistance(Node3D node)
	{
		// TODO: Forward distance
		return 0.0f;
	}

	private void AddChildInEditor(Node parent, Node node)
	{
		// TODO: Add child in editor
	}

	private bool CausedByUserInteraction()
	{
		return Engine.IsEditorHint() && IsNodeReady();
	}

	private void GroupNode(Node node)
	{
		// TODO: Group Node
	}

	private Vector2I CalculateViewportSize()
	{
		// TODO: Calculate viewport size
		return new Vector2I();
	}

	private bool CheckTpInteraction(int flag)
	{
		// TODO; Check tp interaction
		return false;
	}

	private void SetPortalPairUpdateMode(SubViewport.UpdateMode mode)
	{
		// TODO: Set portal pair update mode
	}

	private Vector3 LineIntersection(Vector3 start, Vector3 end)
	{
		// TODO: Line intersection
		return Vector3.Zero;
	}

	#endregion

	#region Godot Editor Integrations

	private string[] GetConfigurationWarnings()
	{
		List<string> warnings = [];

		Vector3 globalScale = GlobalBasis.Scale;
		if (globalScale.IsEqualApprox(Vector3.One))
		{
			warnings.Add
			(
				$"Portals should NOT be scaled. Global portal scale is {globalScale}, " +
				$"but should be {Vector3.One}. Make sure the portal and any of the " +
				"portals parents aren't scaled. "
			);
		}

		if (ExitPortal == null)
		{
			warnings.Add("Exit portal is null. ");
		}

		if (ExitPortal != null && !PortalSize.IsEqualApprox(ExitPortal.PortalSize))
		{
			warnings.Add
			(
				"Portal size should be the same as the exit portals size. " +
				$"Portal size: {PortalSize} Shoulde be: {ExitPortal.PortalSize}"
			);
		}

		return [.. warnings];
	}

	#endregion
}
#endif