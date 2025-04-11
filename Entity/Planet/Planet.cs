using Godot;
using System;

[Tool]
public partial class Planet : CelestialBody
{
    #region Nodes

    private SphereGenerator _generator;

    private const string GeneratorNodeName = "Generator";
    private const string CollisionShapeNodeName = "CollisionShape3D";
    private const string MeshInstanceNodeName = "MeshInstance3D";

    #endregion

    #region Exports

    [Export]
    public PlanetHeight TerrainModifier
    {
        get => _generator?.TerrainModifier;
        set
        {
            if (_generator == null)
            {
                GD.PushWarning($"Planet '{Name}': Cannot set TerrainModifier, Generator node not found.");
                return;
            }

            if (_generator.TerrainModifier == value) return;
            _generator.TerrainModifier = value;

            if (Engine.IsEditorHint())
            {
                UpdateConfigurationWarnings();
            }
        }
    }

    [Export(PropertyHint.Range, "0,200,1")]
    public int Resolution
    {
        get => _generator?.Resolution ?? 10;
        set
        {
            if (_generator == null)
            {
                GD.PushWarning("Planet: Cannot set Resolution, Generator node not found.");
                return;
            }

            if (_generator.Resolution == value) return;
            _generator.Resolution = value;
        }
    }

    [Export(PropertyHint.Range, "0.1,100.0,0.1")]
    public new float Radius
    {
        get => _generator?.Radius ?? base.Radius;
        set
        {
            if (Mathf.IsEqualApprox(base.Radius, value)) return;
            base.Radius = value;

            if (_generator != null && !Mathf.IsEqualApprox(_generator.Radius, value))
            {
                _generator.Radius = value;
            }
            else if (_generator == null)
            {
                GD.PushWarning(
                    "Planet: Cannot set Radius on Generator, Generator node not found. Base Radius updated.");
            }
        }
    }

    private bool _generateCollision = true;

    [Export]
    public bool GenerateCollision
    {
        get => _generateCollision;
        set
        {
            if (_generateCollision == value) return;
            _generateCollision = value;
            RequestUpdate();
        }
    }

    [Export(PropertyHint.File, "*.gdshader")]
    public string PlanetShaderPath { get; set; } = "";

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float PlanetRoughness { get; set; } = 0.8f;

    #endregion

    #region Godot Lifecycle Methods

    public override void _EnterTree()
    {
        base._EnterTree();
        FindRequiredNodes();
    }

    public override void _Ready()
    {
        base._Ready();

        FindRequiredNodes();
        ConnectGeneratorSignals();
        RequestUpdate();
    }

    public override void _ExitTree()
    {
        DisconnectGeneratorSignals();
        base._ExitTree();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new System.Collections.Generic.List<string>();
        warnings.AddRange(base._GetConfigurationWarnings() ?? []);

        if (GetNodeOrNull<SphereGenerator>(GeneratorNodeName) == null)
        {
            warnings.Add($"Planet requires a child node of type SphereGenerator named '{GeneratorNodeName}'.");
        }
        else if (_generator?.TerrainModifier == null)
        {
            warnings.Add(
                $"Assign a PlanetHeight resource to the '{GeneratorNodeName}' node's Terrain Modifier property.");
        }

        if (CollisionShape == null)
        {
            warnings.Add($"Required node '{CollisionShapeNodeName}' (CollisionShape3D) is missing.");
        }

        if (MeshInstance == null)
        {
            warnings.Add($"Required node '{MeshInstanceNodeName}' (MeshInstance3D) is missing.");
        }

        return warnings.ToArray();
    }

    #endregion

    #region Node Management

    private void FindRequiredNodes()
    {
        _generator = GetNodeOrNull<SphereGenerator>(GeneratorNodeName);
        MeshInstance = GetNodeOrNull<MeshInstance3D>(MeshInstanceNodeName);
        CollisionShape = GetNodeOrNull<CollisionShape3D>(CollisionShapeNodeName);

        if (Engine.IsEditorHint())
        {
            UpdateConfigurationWarnings();
        }
    }

    private void ConnectGeneratorSignals()
    {
        if (_generator == null) return;

        if (_generator.IsConnected(SphereGenerator.SignalName.ParametersChanged, Callable.From(RequestUpdate))) return;
        var err = _generator.Connect(SphereGenerator.SignalName.ParametersChanged, Callable.From(RequestUpdate));
        if (err != Error.Ok)
            GD.PushError($"Planet: Failed to connect to Generator's ParametersChanged signal: {err}");
    }

    private void DisconnectGeneratorSignals()
    {
        if (_generator != null &&
            _generator.IsConnected(SphereGenerator.SignalName.ParametersChanged, Callable.From(RequestUpdate)))
        {
            _generator.Disconnect(SphereGenerator.SignalName.ParametersChanged, Callable.From(RequestUpdate));
        }
    }

    #endregion

    #region Planet Update Logic

    public void RequestUpdate()
    {
        CallDeferred(nameof(UpdatePlanetGeometry));
    }

    private void UpdatePlanetGeometry()
    {
        FindRequiredNodes();

        if (_generator == null || MeshInstance == null || CollisionShape == null)
        {
            GD.PrintErr("Planet: Cannot update geometry, required nodes missing.");
            base.UpdateShapeAndMesh();
            return;
        }

        GD.Print($"Planet '{Name}': Updating geometry...");

        var generatedMesh = _generator.GenerateMeshData();

        if (generatedMesh == null || generatedMesh.GetSurfaceCount() == 0)
        {
            GD.PushWarning($"Planet '{Name}': Mesh generation failed or resulted in an empty mesh.");
            MeshInstance.Mesh = null;
            CollisionShape.Shape = null;
            MeshInstance.SetSurfaceOverrideMaterial(0, null);
            return;
        }

        MeshInstance.Mesh = generatedMesh;
        ApplyMaterialAndParameters();

        if (GenerateCollision)
        {
            var collisionShapeData = generatedMesh.CreateTrimeshShape();

            if (collisionShapeData != null)
            {
                CollisionShape.Shape = collisionShapeData;
                GD.Print($"Planet '{Name}': Applied Trimesh collision shape.");
            }
            else
            {
                GD.PushWarning($"Planet '{Name}': Failed to create trimesh collision shape.");
                CollisionShape.Shape = null;
            }
        }
        else
        {
            CollisionShape.Shape = null;
            GD.Print($"Planet '{Name}': Collision shape removed (GenerateCollision is false).");
        }

        if (!Engine.IsEditorHint() && SurfaceGravity > 0.001f && GravitationalConstant > 0.001f && Radius > 0.01f)
        {
            Mass = SurfaceGravity * (Radius * Radius) / GravitationalConstant;
        }
    }

    private void ApplyMaterialAndParameters()
    {
        if (MeshInstance == null)
        {
            GD.PushWarning($"Planet '{Name}': Cannot apply material, MeshInstance3D is null.");
            return;
        }

        if (string.IsNullOrEmpty(PlanetShaderPath))
        {
            GD.PushWarning($"Planet '{Name}': Cannot apply material, PlanetShaderPath is not set.");
            MeshInstance.SetSurfaceOverrideMaterial(0, null);
            return;
        }

        var shader = GD.Load<Shader>(PlanetShaderPath);
        if (shader == null)
        {
            GD.PushError($"Planet '{Name}': Failed to load shader from path: {PlanetShaderPath}");
            MeshInstance.SetSurfaceOverrideMaterial(0, null);
            return;
        }

        if (MeshInstance.GetSurfaceOverrideMaterial(0) is not ShaderMaterial currentMaterial) return;
        currentMaterial.SetShaderParameter("base_radius", Radius);
        currentMaterial.SetShaderParameter("roughness", PlanetRoughness);
    }

    protected override void UpdateShapeAndMesh()
    {
        if (!IsInsideTree()) return;

        if (_generator == null)
        {
            GD.PushWarning(
                $"Planet '{Name}': No Generator found. Falling back to basic CelestialBody sphere generation.");
            base.UpdateShapeAndMesh();
            MeshInstance?.SetSurfaceOverrideMaterial(0, null);
        }
        else
        {
            RequestUpdate();
        }
    }

    #endregion
}