using Godot;

[Tool]
public partial class CelestialBody : RigidBody3D
{
    [Export] public float SurfaceGravity = 10.0f;

    [Export] public float InitialMass = 1000.0f;

    [Export] public Vector3 InitialVelocity = Vector3.Zero;

    [Export] public float GravitationalConstant = 6.6743e-11f;

    [Export] public CelestialBody OrbitParent;

    [Export] public bool AutoCalculateOrbitalVelocity;

    private float _radius = 5.0f;

    [Export]
    public float Radius
    {
        get => _radius;
        set
        {
            if (Mathf.IsEqualApprox(_radius, value))
                return;
            _radius = Mathf.Max(0.01f, value);

            if (IsInsideTree())
            {
                UpdateShapeAndMesh();
            }
        }
    }

    [Export] protected CollisionShape3D _collisionShape;

    [Export] protected MeshInstance3D _meshInstance;

    private const string CelestialGroup = "celestial_bodies";

    public override void _Ready()
    {
        if (_collisionShape == null)
        {
            _collisionShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
            if (_collisionShape == null)
                GD.PrintErr(
                    $"{Name}: CollisionShape3D node not found or assigned. Physics might not work correctly."
                );
        }

        if (_meshInstance == null)
        {
            _meshInstance = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        }

        UpdateShapeAndMesh();

        if (Engine.IsEditorHint())
            return;

        if (SurfaceGravity > 0.001f && GravitationalConstant > 0.001f && Radius > 0.01f)
        {
            Mass = SurfaceGravity * (Radius * Radius) / GravitationalConstant;
        }
        else
        {
            Mass = InitialMass;
        }

        if (AutoCalculateOrbitalVelocity && OrbitParent != null)
        {
            OrbitalVelocity();
        }
        else
        {
            LinearVelocity = InitialVelocity;
        }

        PrintInitialState();

        ContactMonitor = true;
        MaxContactsReported = 8;
        AddToGroup(CelestialGroup);
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (Engine.IsEditorHint())
            return;

        var bodies = GetTree().GetNodesInGroup(CelestialGroup);
        var totalGravitationalForce = Vector3.Zero;
        var thisPosition = state.Transform.Origin;

        foreach (var node in bodies)
        {
            if (node == this)
                continue;

            if (node is not CelestialBody otherBody || !IsInstanceValid(otherBody))
                continue;

            var otherPosition = otherBody.GlobalPosition;

            var offset = otherPosition - thisPosition;
            var sqrDist = offset.LengthSquared();

            const float epsilonSq = 0.001f * 0.001f;
            if (sqrDist < epsilonSq)
                continue;

            var forceDir = offset.Normalized();

            if (this.Mass <= 0 || otherBody.Mass <= 0)
                continue;

            var forceMagnitude = GravitationalConstant * this.Mass * otherBody.Mass / sqrDist;
            var forceVector = forceDir * forceMagnitude;

            totalGravitationalForce += forceVector;
        }

        state.ApplyCentralForce(totalGravitationalForce);
    }

    protected void OrbitalVelocity()
    {
        if (OrbitParent == null || !IsInstanceValid(OrbitParent))
        {
            GD.PrintErr(
                $"{Name}: Cannot calculate orbital velocity - OrbitParent is null or invalid."
            );

            return;
        }

        var positionOffset = GlobalPosition - OrbitParent.GlobalPosition;
        var distance = positionOffset.Length();

        if (distance < 0.001f)
        {
            GD.PrintErr(
                $"{Name}: Cannot calculate orbital velocity - Too close to parent body {OrbitParent.Name}!"
            );

            LinearVelocity = OrbitParent.LinearVelocity;
            return;
        }

        if (OrbitParent.Mass < 0.001f)
        {
            GD.PrintErr(
                $"{Name}: Cannot calculate orbital velocity - Parent body {OrbitParent.Name} has negligible mass!"
            );
            LinearVelocity = OrbitParent.LinearVelocity;
            return;
        }

        if (GravitationalConstant < 0.00001f)
        {
            GD.PrintErr(
                $"{Name}: Cannot calculate orbital velocity - GravitationalConstant is too small!"
            );
            LinearVelocity = OrbitParent.LinearVelocity;
            return;
        }

        var velocityMagnitude = Mathf.Sqrt(GravitationalConstant * OrbitParent.Mass / distance);

        var orbitDirection = Vector3.Up;
        if (Mathf.Abs(positionOffset.Normalized().Dot(Vector3.Up)) > 0.99f)
        {
            orbitDirection = Vector3.Forward;
        }

        var velocityDirection = positionOffset.Cross(orbitDirection).Normalized();

        var orbitalVelocityRelative = velocityDirection * velocityMagnitude;

        LinearVelocity = orbitalVelocityRelative + OrbitParent.LinearVelocity;

        GD.Print(
            $"{Name}: Calculated orbital velocity around {OrbitParent.Name}: {LinearVelocity.Length()} m/s (Relative component: {orbitalVelocityRelative.Length()} m/s)"
        );
    }

    protected virtual void UpdateShapeAndMesh()
    {
        if (_collisionShape?.Shape is SphereShape3D sphereShape)
        {
            sphereShape.Radius = _radius;
        }
        else if (_collisionShape != null)
        {
            if (_collisionShape.Shape != null)
            {
                GD.PrintErr(
                    $"{Name}: CollisionShape node exists but its Shape is not a SphereShape3D. Cannot auto-update radius based on CelestialBody.Radius."
                );
            }
            else
            {
                GD.PrintErr(
                    $"{Name}: CollisionShape node exists but has no Shape resource assigned."
                );
            }
        }

        if (_meshInstance?.Mesh is SphereMesh sphereMesh)
        {
            sphereMesh.Radius = _radius;
            sphereMesh.Height = _radius * 2.0f;
        }
    }

    private void PrintInitialState()
    {
        GD.Print($"--- Initial State for {Name} ---");
        GD.Print($"  Position: {GlobalPosition}");
        GD.Print($"  Radius: {Radius}");
        GD.Print($"  SurfaceGravity: {SurfaceGravity}");
        GD.Print($"  Calculated/Set Mass: {Mass}");
        GD.Print($"  Initial/Calculated Velocity: {LinearVelocity}");
        if (OrbitParent != null)
        {
            GD.Print($"  Orbiting: {OrbitParent.Name}");
            var dist = (GlobalPosition - OrbitParent.GlobalPosition).Length();
            GD.Print($"  Distance to Parent: {dist}");
            if (OrbitParent.Mass > 0)
                GD.Print($"  Mass Ratio (Self/Parent): {Mass / OrbitParent.Mass}");
        }
        else
        {
            GD.Print("  Not Orbiting (No Parent Assigned)");
        }

        GD.Print($"---------------------------------");
    }
}