using Godot;

public partial class CelestialBody : RigidBody3D
{
	[Export] public float InitialMass = 500.0f;
	[Export] public float Radius = 5.0f;
	[Export] public Vector3 InitialVelocity = Vector3.Zero;
	[Export] public float GravitationalConstant = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");

	[Export] private MeshInstance3D _meshInstance;
	[Export] private CollisionShape3D _collisionShape;

	private const string CelestialGroup = "celestial_bodies";
	private bool _initialized;

	public override void _Ready()
	{
		Mass = InitialMass;
		
		AddToGroup(CelestialGroup);

		var mesh = (SphereMesh)_meshInstance.Mesh;
		mesh.Radius = Radius;
		mesh.Height = Radius * 2.0f;

		var collisionShape = (SphereShape3D)_collisionShape.Shape;
		collisionShape.Radius = Radius;

		LinearVelocity = InitialVelocity;
		
		ContactMonitor = true;
		MaxContactsReported = 8;
	}
	
	public override void _IntegrateForces(PhysicsDirectBodyState3D state)
	{
		var bodies = GetTree().GetNodesInGroup(CelestialGroup);

		var totalGravitationalForce = Vector3.Zero;

		foreach (var node in bodies)
		{
			if (node == this) continue;

			if (node is not CelestialBody otherBody || !IsInstanceValid(otherBody)) continue;

			var otherPosition = otherBody.GlobalPosition;
			var thisPosition = state.Transform.Origin;

			var offset = otherPosition - thisPosition;
			var sqrDist = offset.LengthSquared();
			
			var forceDir = offset.Normalized();
			var forceMagnitude = GravitationalConstant * Mass * otherBody.Mass / sqrDist;
			
			var forceVector = forceDir * forceMagnitude;
			
			totalGravitationalForce += forceVector;
		}
		
		state.ApplyCentralForce(totalGravitationalForce);
	}
}
