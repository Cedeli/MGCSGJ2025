using Godot;

[Tool]
public partial class CelestialBody : RigidBody3D
{
    [Export] public float InitialMass = 500.0f;
    [Export] public Vector3 InitialVelocity = Vector3.Zero;
    [Export] public float GravitationalConstant = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
    
    private float _radius = 5.0f;
    [Export]
    public float Radius
    {
        get => _radius;
        set
        {
            _radius = value;
            if (IsInsideTree())
            {
                UpdateShapeAndMesh();
            }
        }
    }

    [Export] private MeshInstance3D _meshInstance;
    [Export] private CollisionShape3D _collisionShape;

    private const string CelestialGroup = "celestial_bodies";

    public override void _Ready()
    {
        UpdateShapeAndMesh();
        
        if (Engine.IsEditorHint()) return;
        Mass = InitialMass;
        LinearVelocity = InitialVelocity;
        ContactMonitor = true;
        MaxContactsReported = 8;
        AddToGroup(CelestialGroup);
    }
    
    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (Engine.IsEditorHint()) return;
        
        var bodies = GetTree().GetNodesInGroup(CelestialGroup);
        var totalGravitationalForce = Vector3.Zero;
        var thisPosition = state.Transform.Origin;

        foreach (var node in bodies)
        {
            if (node == this) continue;

            if (node is not CelestialBody otherBody || !IsInstanceValid(otherBody)) continue;

            var otherPosition = otherBody.GlobalPosition;

            var offset = otherPosition - thisPosition;
            var sqrDist = offset.LengthSquared();
            
            if (sqrDist < 0.0001f) continue;
            
            var forceDir = offset.Normalized();
            var forceMagnitude = GravitationalConstant * Mass * otherBody.Mass / sqrDist;
            var forceVector = forceDir * forceMagnitude;
            
            totalGravitationalForce += forceVector;
        }
        
        state.ApplyCentralForce(totalGravitationalForce);
    }
    
    private void UpdateShapeAndMesh()
    {
        if (_meshInstance == null || _collisionShape == null)
        {
            GD.PrintErr("MeshInstance or CollisionShape not assigned!");
            return;
        }

        if (_meshInstance.Mesh is SphereMesh sphereMesh)
        {
            sphereMesh.Radius = _radius;
            sphereMesh.Height = _radius * 2.0f;
        }
        else if (_meshInstance.Mesh != null)
        {
            GD.PrintErr($"MeshInstance '{_meshInstance.Name}' does not have a SphereMesh assigned.");
        }

        if (_collisionShape.Shape is SphereShape3D sphereShape)
        {
            sphereShape.Radius = _radius;
        }
        else if (_collisionShape.Shape != null)
        {
            GD.PrintErr($"CollisionShape '{_collisionShape.Name}' does not have a SphereShape3D assigned.");
        }
    }
}