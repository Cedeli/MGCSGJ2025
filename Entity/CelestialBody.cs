using Godot;

[Tool]
public partial class CelestialBody : RigidBody3D
{
    [Export] public float SurfaceGravity = 10.0f;
    [Export] public float InitialMass = 1000.0f;
    [Export] public Vector3 InitialVelocity = Vector3.Zero;
    [Export] public float GravitationalConstant = 0.1f;
    [Export] public CelestialBody OrbitParent;
    [Export] public bool AutoCalculateOrbitalVelocity;

    private float _gravitationalMass;
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
        
        if (AutoCalculateOrbitalVelocity && OrbitParent != null)
        {
            OrbitalVelocity();
            Mass = SurfaceGravity * (Radius * Radius) / GravitationalConstant;
        }
        else
        {
            LinearVelocity = InitialVelocity;
            Mass = InitialMass;
        }
        
        var positionOffset = GlobalPosition - OrbitParent.GlobalPosition;
        var distance = positionOffset.Length();
        _gravitationalMass = SurfaceGravity * Radius * Radius;
        
        GD.Print($"Global position for {Name} is: {GlobalPosition}");
        GD.Print($"Mass for { Name }: { Mass }");
        GD.Print($"Distance from {Name} to {OrbitParent.Name}: {distance} units");
        
        ContactMonitor = true;
        MaxContactsReported = 8;
        AddToGroup(CelestialGroup);
    }
    
    private void OrbitalVelocity()
    {
        if (OrbitParent == null) return;
    
        var positionOffset = GlobalPosition - OrbitParent.GlobalPosition;
        var distance = positionOffset.Length();
    
        if (distance < 0.001f)
        {
            GD.PrintErr("Cannot calculate orbital velocity: Too close to parent body!");
            return;
        }
        
        var velocityMagnitude = Mathf.Sqrt(GravitationalConstant * OrbitParent.Mass / distance);

        var orbitDirection = Mathf.Abs(positionOffset.Normalized().Dot(Vector3.Up)) > 0.99f ? 
            positionOffset.Cross(Vector3.Forward).Normalized() : 
            positionOffset.Cross(Vector3.Up).Normalized();
        
        LinearVelocity = orbitDirection * velocityMagnitude;
        LinearVelocity += OrbitParent.LinearVelocity;
    
        GD.Print($"Orbital velocity for {Name} around {OrbitParent.Name}: {LinearVelocity.Length()} m/s");
        GD.Print($"Mass ratio {Name}/{OrbitParent.Name}: {Mass/OrbitParent.Mass}");
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

    public Vector3 GetAccelerationAtPosition(Vector3 globalPosition)
    {
        var distance = globalPosition - this.GlobalPosition;
        return distance.Normalized() * _gravitationalMass / distance.LengthSquared();
    }
}