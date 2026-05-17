#if TOOLS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using Godot.Collections;
namespace Portals3D;

/// <summary>
/// <para>
/// To get started, create two Portal3D instances and set their <c>Exit Portal</c> to each other.
/// </para>
/// <para>
/// This creates a linked portal pair that you can look through. Make your player to collide with
/// <c>TeleportCollisionMask</c> and you will be able to walk back and forth through the portal.
/// </para>
/// <para>
/// To integrate portals into your game, you can make use of the <b>Signals</b> <c>OnTeleport</c> and <c>OnTeleportRecevie</c> during gameplay.
/// </para>
/// <para>
/// The next level is to make use of the portal's callbacks, mainly <c>OnTeleportCallback</c>.
/// </para>
/// <para>
/// If you need to raycast through a portal checkout the <c>ForwardRaycast</c> and <c>ForwardRaycastQuery</c> methods.
/// </para>
/// <para>
/// For optimization, use the <c>Activate</c> and <c>Deactivate</c> methods to control which portals are consuming resources.
/// </para>
/// <para>
/// <em><b>TIP:</b> For easy defaults management of various portals, create a scene with Portal3D and the root and re-use that scene instead.</em>
/// </para>
/// </summary>
[Tool, Icon("uid://d22d43uoy7fnv"), GlobalClass]
public partial class Portal3D : Node3D
{
	#region Public API

	/// <summary>
	/// Emitted when this portal teleports something. Also see signal <c>OnTeleportReceive</c>.
	/// </summary>
	/// <param name="node"></param>
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
			[this.TeleportArea.GetRid(), ExitPortal.TeleportArea.GetRid()]
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

	private static readonly StringName OnTeleportCallback = new("OnTeleport");
	private static readonly StringName DuplicateMeshesCallback = new("GetTeleportableMeshes");
	private static readonly StringName TeleportRootMeta = new("TeleportRoot");

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

	public Callable _TbPairPortals = Callable.From(() => MethodName.EditorPairPortals);
	public Callable _TbSyncPortalSizes = Callable.From(() => MethodName.EditorSyncPortalSizes);

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
	[Export]
	public bool StartDeactivated
	{
		get => _startDeactivated;
		set => _startDeactivated = value;
	}

	#endregion

	#region Internal

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
	public NodePath PortalMeshPath
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

	internal Godot.Collections.Dictionary<ulong, TeleportableMetadata> WatchlistTeleportables = [];

	#endregion

	#region Editor Configuration

	private static readonly Shader PortalShader = GD.Load<Shader>("uid://csiava4euv75d");
	private static readonly StandardMaterial3D EditorPreviewMaterial = GD.Load<StandardMaterial3D>("uid://suwscljyisas");

	private void EditorReady()
	{
		AddToGroup((StringName)PortalSettings.GetSetting("PortalsGroupName"), true);
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

	private void EditorPairPortals()
	{
		Debug.Assert(ExitPortal != null, "My own exit has to be set!");
		ExitPortal.ExitPortal = this;
		NotifyPropertyListChanged();
	}

	private void EditorSyncPortalSizes()
	{
		Debug.Assert(ExitPortal != null, "My own exit has to be set!");
		PortalSize = ExitPortal.PortalSize;
		NotifyPropertyListChanged();
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

		AddChildInEditor(this, area);
		TeleportAreaPath = GetPathTo(area);

		CollisionShape3D collider = new() { Name = "TeleportCollider" };
		BoxShape3D box = new();
		box.Size = box.Size with { X = PortalSize.X, Y = PortalSize.Y };
		collider.Shape = box;

		AddChildInEditor(TeleportArea, collider);
		TeleportColliderPath = GetPathTo(collider);
	}

	private void OnPortalSizeChanged()
	{
		if (PortalMesh == null)
		{
			GD.PushError("Failed to update portal size, portal has no mesh");
			return;	
		}

		PortalBoxMesh pbm = (PortalBoxMesh)PortalMesh.Mesh;
		pbm.Size = new Vector3(PortalSize.X, PortalSize.Y, 1);
		PortalMesh.Scale = PortalMesh.Scale with { Z = PortalThickness };

		if (IsTeleport && TeleportCollider != null)
		{
			BoxShape3D boxShape = (BoxShape3D)TeleportCollider.Shape;
			boxShape.Size = boxShape.Size with { X = PortalSize.X, Y = PortalSize.Y };
		}
	}

	#endregion

	#region Gameplay Logic

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
		{
			CallDeferred("EditorReady");
			return;
		}

		if (PlayerCamera == null)
		{
			PlayerCamera = GetViewport().GetCamera3D();
			Debug.Assert(PlayerCamera != null, "Player camera is missing!");
		}

		ShaderMaterial material = new() { Shader = PortalShader };
		PortalMesh.MaterialOverride = material;

		if (!StartDeactivated)
		{
			SetupCameras();
		}
		else
		{
			CallDeferred("Deactivate", true);
		}

		if (IsTeleport)
		{
			TeleportArea.AreaEntered += OnTeleportAreaEntered;
			TeleportArea.AreaExited += OnTeleportAreaExited;
			TeleportArea.BodyEntered += OnTeleportBodyEntered;
			TeleportArea.BodyExited += OnTeleportBodyExited;
			TeleportArea.CollisionMask = (uint)TeleportCollisionMask;
		}
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint()) return;

		if (IsTeleport) ProcessTeleports();

		ProcessCameras();
	}

	private void ProcessCameras()
	{
		if (PortalCamera == null)
		{
			GD.PushError($"{Name}: No portal camera");
			return;
		}
		if (PlayerCamera == null)
		{
			GD.PushError($"{Name}: No player camera");
			return;
		}
		if (ExitPortal == null)
		{
			GD.PushError($"{Name}: No exit portal");
			return;
		}

		PortalCamera.GlobalTransform = this.ToExitTransform(PlayerCamera.GlobalTransform);
		PortalCamera.Near = CalculateNearPlane();
		PortalCamera.Fov = PlayerCamera.Fov;

		Vector2I pvSize = PortalViewport.Size;
		double degrees = PlayerCamera.Fov * 0.5;
		double halfHeight = PlayerCamera.Near * Math.Tan(degrees * (Math.PI / 180.0));
		double halfWidth = halfHeight * pvSize.X / pvSize.Y;
		float nearDiagonal = new Vector3((float)halfWidth, (float)halfHeight, PlayerCamera.Near).Length();
		PortalMesh.Scale = PortalMesh.Scale with { Z = nearDiagonal };

		bool playerInFrontOfPortal = ForwardDistance(PlayerCamera) > 0;
		float portalShift = 0.0f;
		switch (ViewDirection)
		{
			case PortalViewDirection.OnlyFront:
				portalShift = 1.0f;
				break;

			case PortalViewDirection.OnlyBack:
				portalShift = -1.0f;
				break;

			case PortalViewDirection.FrontAndBack:
				if (playerInFrontOfPortal) portalShift = 1.0f; else portalShift = -1.0f;
				break;
		}

		Vector3 newScale = PortalMesh.Scale;
		newScale.Z *= Math.Sign(portalShift);
		PortalMesh.Scale = newScale;
	}

	private void ProcessTeleports()
	{
		foreach (ulong bodyId in WatchlistTeleportables.Keys)
		{
			if (!IsInstanceIdValid(bodyId))
			{
				EraseTpMetadata(bodyId);
				continue;
			}

			TeleportableMetadata tpMetadata = WatchlistTeleportables[bodyId];
			Node3D body = (Node3D)InstanceFromId(bodyId);
			float lastFwAngle = tpMetadata.Forward;
			float currentFwAngle = ForwardDistance(body);

			bool shouldTeleport = false;
			switch (TeleportDirection)
			{
				case PortalTeleportDirection.Front:
					shouldTeleport = lastFwAngle > 0 && currentFwAngle <= 0;
					break;

				case PortalTeleportDirection.Back:
					shouldTeleport = lastFwAngle < 0 && currentFwAngle >= 0;
					break;

				case PortalTeleportDirection.FrontAndBack:
					shouldTeleport = Math.Sign(lastFwAngle) != Math.Sign(currentFwAngle);
					break;

				default:
					Debug.Assert(false, "This switch should be exhaustive.");
					break;
			}

			if (shouldTeleport && Math.Abs(currentFwAngle) < TeleportTolerance)
			{
				Variant teleportablePath = body.GetMeta(TeleportRootMeta, ".");
				Node3D teleportable = (Node3D)body.GetNode((string)teleportablePath);
				teleportable.GlobalTransform = ToExitTransform(teleportable.GlobalTransform);

				// FIXME: This might not work...it seems like it should but if object redirection/boosting isn't working this is most likley why.
				if (teleportable is RigidBody3D rigidTeleportable)
				{
					rigidTeleportable.LinearVelocity = ToExitDirection(rigidTeleportable.LinearVelocity);
					rigidTeleportable.ApplyCentralImpulse(rigidTeleportable.LinearVelocity.Normalized() * RigidbodyBoost);
				}

				EmitSignal(SignalName.OnTeleport, teleportable);
				ExitPortal.EmitSignal(SignalName.OnTeleportReceive, teleportable);

				if (tpMetadata.IsPlayer)
				{
					ProcessCameras();
					ExitPortal.ProcessCameras();
				}

				if (tpMetadata.IsPlayer && CheckTpInteraction((int)PortalTeleportInteractions.PlayerUpright))
				{
					GetTree().CreateTween().TweenProperty(teleportable, "rotation:x", 0, 0.3);
					GetTree().CreateTween().TweenProperty(teleportable, "rotation:z", 0, 0.3);
				}

				if (CheckTpInteraction((int)PortalTeleportInteractions.Callback))
				{
					if (teleportable.HasMethod(OnTeleportCallback)) teleportable.Call(OnTeleportCallback, this);
				}

				TransferTpMetadataToExit(body);
			}
			else
			{
				tpMetadata.Forward = currentFwAngle;
				UpdateTpCloneTransforms(tpMetadata, this);
			}
		}
	}
	
	private float CalculateNearPlane()
	{
		Aabb aabb = new
		(
			new Vector3(-ExitPortal.PortalSize.X / 2, -ExitPortal.PortalSize.Y / 2, 0),
			new Vector3(ExitPortal.PortalSize.X, ExitPortal.PortalSize.Y, 0)
		);

		Vector3 pos = aabb.Position;
		Vector3 size = aabb.Size;

		Vector3 corner1 = ExitPortal.ToGlobal(new Vector3(pos.X, pos.Y, 0));
		Vector3 corner2 = ExitPortal.ToGlobal(new Vector3(pos.X + size.X, pos.Y, 0));
		Vector3 corner3 = ExitPortal.ToGlobal(new Vector3(pos.X + size.X, pos.Y + size.Y, 0));
		Vector3 corner4 = ExitPortal.ToGlobal(new Vector3(pos.X, pos.Y + size.Y, 0));

		Vector3 cameraForward = -PortalCamera.GlobalTransform.Basis.Z.Normalized();

		float d1 = (corner1 - PortalCamera.GlobalPosition).Dot(cameraForward);
		float d2 = (corner2 - PortalCamera.GlobalPosition).Dot(cameraForward);
		float d3 = (corner3 - PortalCamera.GlobalPosition).Dot(cameraForward);
		float d4 = (corner4 - PortalCamera.GlobalPosition).Dot(cameraForward);

		return Math.Max(0.01f, (float)new[] { d1, d2, d3, d4 }.Min() - ExitPortal.PortalFrameWidth);
	}

	private void SetupMesh()
	{
		if (PortalMesh != null) return;

		MeshInstance3D meshInstance = new()
		{
			Name = this.Name + "_Mesh",
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Layers = PortalRenderLayer
		};

		PortalBoxMesh boxMesh = new()
		{
			Size = new Vector3(PortalSize.X, PortalSize.Y, 1)
		};

		meshInstance.Mesh = boxMesh;
		meshInstance.Scale = meshInstance.Scale with { Z = PortalThickness };

		meshInstance.MaterialOverride = EditorPreviewMaterial;

		AddChildInEditor(this, meshInstance);
		PortalMeshPath = GetPathTo(meshInstance);
	}

	private void SetupCameras()
	{
		Debug.Assert(!Engine.IsEditorHint(), "This should never run in editor.");
		Debug.Assert(PortalCamera == null);
		Debug.Assert(PortalViewport == null);

		if (ExitPortal == null)
		{
			GD.PushError($"{Name} has no exit portal, failed to setup cameras.");
			return;
		}

		PortalViewport = new SubViewport
		{
			Name = this.Name + "_SubViewport",
			Size = CalculateViewportSize()
		};
		AddChild(PortalViewport, true);

		Godot.Environment adjustedEnv = null;
		if (PlayerCamera.Environment != null)
		{
			adjustedEnv = (Godot.Environment)PlayerCamera.Environment.Duplicate();
		}
		else
		{
			adjustedEnv = (Godot.Environment)PlayerCamera.GetWorld3D().Environment.Duplicate();
		}

		adjustedEnv.TonemapMode = Godot.Environment.ToneMapper.Linear;
		adjustedEnv.TonemapExposure = 1;

		PortalCamera = new Camera3D
		{
			Name = this.Name + "_Camera3D",
			Environment = adjustedEnv
		};

		PortalCamera.CullMask ^= PortalRenderLayer;

		PortalViewport.AddChild(PortalCamera, true);
		PortalCamera.GlobalPosition = ExitPortal.GlobalPosition;

		ShaderMaterial material = (ShaderMaterial)PortalMesh.MaterialOverride;
		material.SetShaderParameter("albedo", PortalViewport.GetTexture());

		Viewport vp = GetViewport();
		if (!vp.IsConnected(Viewport.SignalName.SizeChanged, Callable.From(OnWindowResize)))
		{
			vp.SizeChanged += OnWindowResize;
		}
		else
		{
			GD.PushError($"{Name} failed to connect to OnWindowResize signal.");
		}
	}

	#endregion

	#region Event Handlers

	private void OnTeleportAreaEntered(Area3D area)
	{
		if (WatchlistTeleportables.ContainsKey(area.GetInstanceId())) return;

		ConstructTpMetadata(area);
	}

	private void OnTeleportBodyEntered(Node3D body)
	{
		if (WatchlistTeleportables.ContainsKey(body.GetInstanceId())) return;

		ConstructTpMetadata(body);
	}

	private void OnTeleportAreaExited(Area3D area)
	{
		EraseTpMetadata(area.GetInstanceId());
	}

	private void OnTeleportBodyExited(Node3D body)
	{
		EraseTpMetadata(body.GetInstanceId());
	}

	private void OnWindowResize()
	{
		if (PortalViewport != null) PortalViewport.Size = CalculateViewportSize();
	}

	#endregion

	#region UTILS

	private void ConstructTpMetadata(Node3D node)
	{
		Node teleportable = node.GetNode((NodePath)node.GetMeta(TeleportRootMeta, "."));

		TeleportableMetadata metadata = new()
		{
			Forward = ForwardDistance(node),
			IsPlayer = !teleportable.GetPathTo(PlayerCamera).ToString().StartsWith('.')
		};

		if (metadata.IsPlayer) SetPortalPairUpdateMode(SubViewport.UpdateMode.Always);

		if (CheckTpInteraction((int)PortalTeleportInteractions.DuplicateMeshes)
		&& node.HasMethod(DuplicateMeshesCallback))
		{
			metadata.Meshes = (Array<MeshInstance3D>)node.Call(DuplicateMeshesCallback);
			foreach (MeshInstance3D mesh in metadata.Meshes)
			{
				MeshInstance3D dupeMesh = (MeshInstance3D)mesh.Duplicate(0);
				dupeMesh.Name = mesh.Name + "_Clone";
				metadata.MeshClones.Add(dupeMesh);
				AddChild(dupeMesh, true);
			}
			EnableMeshClipping(metadata, this);
		}
		WatchlistTeleportables.TryAdd(node.GetInstanceId(), metadata);
	}

	private void EraseTpMetadata(ulong nodeId)
	{
		// FIXME: Double lookup here, this is preventing an error
		//	where the lookup assignment 1 line below this is throws
		//	a key not found exception...maybe the method is getting called
		//	multiple times?
		if (!WatchlistTeleportables.ContainsKey(nodeId)) return;

		TeleportableMetadata metadata = WatchlistTeleportables[nodeId];
		if (metadata != null)
		{
			if (metadata.IsPlayer) SetPortalPairUpdateMode(SubViewport.UpdateMode.WhenVisible);

			foreach (MeshInstance3D mesh in metadata.Meshes) DisableMeshClipping(mesh);
			foreach (MeshInstance3D meshClone in metadata.MeshClones) meshClone.QueueFree();
		}
		if (!WatchlistTeleportables.Remove(nodeId)) return;
	}

	private void TransferTpMetadataToExit(Node3D body)
	{
		ulong bodyId = body.GetInstanceId();
		TeleportableMetadata metadata = WatchlistTeleportables[bodyId];
		Debug.Assert(metadata != null, "Attempted to transfer teleport metadata for a node that is not being watched.");

		// Self-teleport edge case to keep metadata and refresh clipping.
		if (ExitPortal == this)
		{
			metadata.Forward = ForwardDistance(body);
			EnableMeshClipping(metadata, this);
			UpdateTpCloneTransforms(metadata, this);
			return;
		}

		if (!ExitPortal.IsTeleport) return; // If one way teleport.

		metadata.Forward = ExitPortal.ForwardDistance(body);
		EnableMeshClipping(metadata, ExitPortal);
		UpdateTpCloneTransforms(metadata, ExitPortal);

		ExitPortal.WatchlistTeleportables.TryAdd(bodyId, metadata);

		if (metadata.IsPlayer && ExitPortal.ExitPortal != this)
		{
			PortalViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible;
			ExitPortal.SetPortalPairUpdateMode(SubViewport.UpdateMode.Always);
		}
		WatchlistTeleportables.Remove(bodyId);
	}
	
	private void UpdateTpCloneTransforms(TeleportableMetadata metadata, Portal3D portal)
	{
		for (int i = 0; i < metadata.MeshClones.Count; i++)
		{
			MeshInstance3D mesh = metadata.Meshes[i];
			MeshInstance3D clone = metadata.MeshClones[i];
			clone.GlobalTransform = portal.ToExitTransform(mesh.GlobalTransform);
		}
	}

	private void EnableMeshClipping(TeleportableMetadata metadata, Portal3D alongPortal)
	{
		foreach (MeshInstance3D meshInstance in metadata.Meshes)
		{
			Vector3 clipNormal = Math.Sign(metadata.Forward) * alongPortal.GlobalBasis.Z;
			meshInstance.SetInstanceShaderParameter("portal_clip_active", true);
			meshInstance.SetInstanceShaderParameter("portal_clip_point", alongPortal.GlobalPosition);
			meshInstance.SetInstanceShaderParameter("portal_clip_normal", clipNormal);
		}

		Portal3D exitPortal = alongPortal.ExitPortal;
		foreach (MeshInstance3D meshClone in metadata.MeshClones)
		{
			Vector3 clipNormal = Math.Sign(metadata.Forward) * exitPortal.GlobalBasis.Z;
			meshClone.SetInstanceShaderParameter("portal_clip_active", true);
			meshClone.SetInstanceShaderParameter("portal_clip_point", exitPortal.GlobalPosition);
			meshClone.SetInstanceShaderParameter("portal_clip_normal", clipNormal);
		}
	}

	private void DisableMeshClipping(MeshInstance3D meshInstance)
	{
		meshInstance.SetInstanceShaderParameter("portal_clip_active", false);
	}

	private Transform3D ToExitTransform(Transform3D gTransform)
	{
		Transform3D relativeToPortal = GlobalTransform.AffineInverse() * gTransform;
		Transform3D flippedTransform = relativeToPortal.Rotated(Vector3.Up, (float)Math.PI);
		Transform3D relativeToTarget = ExitPortal.GlobalTransform * flippedTransform;
		return relativeToTarget;
	}

	private Vector3 ToExitDirection(Vector3 real)
	{
		Vector3 relativeToPortal = GlobalBasis.Inverse() * real;
		Vector3 flippedVector = relativeToPortal.Rotated(Vector3.Up, (float)Math.PI);
		Vector3 relativeToTarget = ExitPortal.GlobalBasis * flippedVector;
		return relativeToTarget;
	}

	private Vector3 ToExitPosition(Vector3 gPos)
	{
		Vector3 localVector = GlobalTransform.AffineInverse() * gPos;
		Vector3 rotatedVector = localVector.Rotated(Vector3.Up, (float)Math.PI);
		Vector3 localAtExit = ExitPortal.GlobalTransform * rotatedVector;
		return localAtExit;
	}

	private float ForwardDistance(Node3D node)
	{
		Vector3 portalFront = this.GlobalTransform.Basis.Z.Normalized();
		Vector3 nodeRelative = node.GlobalTransform.Origin - this.GlobalTransform.Origin;
		return portalFront.Dot(nodeRelative);
	}

	private void AddChildInEditor(Node parent, Node node)
	{
		parent.AddChild(node, true);
		if (this.Owner == null)
		{
			node.Owner = this;
		}
		else
		{
			node.Owner = this.Owner;
		}
	}

	private bool CausedByUserInteraction()
	{
		return Engine.IsEditorHint() && IsNodeReady();
	}

	private void GroupNode(Node node)
	{
		node.SetMeta("_edit_group_", true);
	}

	private Vector2I CalculateViewportSize()
	{
		Vector2I viewportSize = (Vector2I)GetViewport().GetVisibleRect().Size;
		float aspectRatio = viewportSize.X / viewportSize.Y;

		switch (ViewportSizeMode)
		{
			case PortalViewportSizeMode.Full:
				return viewportSize;

			case PortalViewportSizeMode.MaxWidthAbsolute:
				int width = Math.Min(ViewportSizeMaxWidthAbsolute, viewportSize.X);
				return new Vector2I(width, (int)(width / aspectRatio));

			case PortalViewportSizeMode.Fractional:
				Vector2 calculateFractional = (Vector2)viewportSize * ViewportSizeFractional;
				return (Vector2I)calculateFractional;
		}

		GD.PushError("Failed to determine desired viewport size.");
		return new Vector2I
		(
			(int)ProjectSettings.GetSetting("display/window/size/viewport_width"),
			(int)ProjectSettings.GetSetting("display/window/size/viewport_height")
		);
	}

	private bool CheckTpInteraction(int flag)
	{
		return (TeleportInteractions & flag) > 0;
	}

	private void SetPortalPairUpdateMode(SubViewport.UpdateMode mode)
	{
		Debug.Assert(IsInstanceIdValid(ExitPortal.GetInstanceId()));
		this.PortalViewport.RenderTargetUpdateMode = mode;
		if (ExitPortal.PortalViewport != null) ExitPortal.PortalViewport.RenderTargetUpdateMode = mode;
	}

	private Vector3 LineIntersection(Vector3 start, Vector3 end)
	{
		Vector3 planeNormal = -GlobalBasis.Z;
		Vector3 planePoint = GlobalPosition;

		Vector3 lineDir = end - start;
		float denom = planeNormal.Dot(lineDir);

		if (Math.Abs(denom) < 1e-6) return Vector3.Zero;

		float t = planeNormal.Dot(planePoint - start) / denom;
		return start + lineDir * t;
	}

	#endregion

	#region Godot Editor Integrations

	public override string[] _GetConfigurationWarnings()
	{
		List<string> warnings = [];

		Vector3 globalScale = GlobalBasis.Scale;
		if (!globalScale.IsEqualApprox(Vector3.One))
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

		base._GetConfigurationWarnings();

		return [.. warnings];
	}

	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> config = [];

		if (ExitPortal != null && !PortalSize.IsEqualApprox(ExitPortal.PortalSize))
		{
			config.Add(AtExport.ExportButton("_TbSyncPortalSizes", "Take Exit Portal's Size", "Vector2"));
		}

		if (ExitPortal != null && ExitPortal.ExitPortal == null)
		{
			config.Add(AtExport.ExportButton("_TbPairPortals", "Pair Portals", "SliderJoint3D"));
		}

		config.Add(new Dictionary()
		{
			{"name", "PortalThickness"},
			{"type", (int)Variant.Type.Float},
			{"usage", (int)PropertyUsageFlags.Storage}
		});
		config.Add(new Dictionary()
		{
			{"name", "PortalMeshPath"},
			{"type", (int)Variant.Type.NodePath},
			{"usage", (int)PropertyUsageFlags.Storage}
		});
		config.Add(new Dictionary()
		{
			{"name", "TeleportAreaPath"},
			{"type", (int)Variant.Type.NodePath},
			{"usage", (int)PropertyUsageFlags.Storage}
		});
		config.Add(new Dictionary()
		{
			{"name", "TeleportColliderPath"},
			{"type", (int)Variant.Type.NodePath},
			{"usage", (int)PropertyUsageFlags.Storage}
		});

		base._GetPropertyList();

		return config;
	}

	#endregion
}
#endif