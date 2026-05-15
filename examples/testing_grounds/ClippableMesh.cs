using Godot;

public partial class ClippableMesh : RigidBody3D
{
	[Export] public MeshInstance3D SelfMesh;

	public Godot.Collections.Array<MeshInstance3D> GetTeleportableMeshes()
	{
		Godot.Collections.Array<MeshInstance3D> result = [SelfMesh];
		return result;
	}
}
