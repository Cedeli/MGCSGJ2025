using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class WaterSphere : Node3D
{
    private MeshInstance3D _waterMeshInstance;
    private Planet _parentPlanet;

    private const string WaterMeshInstanceNodeName = "WaterMeshInstance3D";

    [Export] public Shader WaterShaderResource { get; set; }

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float WaterLevel { get; set; } = 0.98f;

    [Export(PropertyHint.ColorNoAlpha)] public Color ShallowColorValue { get; set; } = new(0.25f, 0.75f, 1.0f);

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float ShallowColorAlpha { get; set; } = 0.7f;

    [Export(PropertyHint.ColorNoAlpha)] public Color DeepColorValue { get; set; } = new(0.0f, 0.15f, 0.4f);

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float DeepColorAlpha { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.1, 20.0, 0.1")]
    public float DepthDistance { get; set; } = 5.0f;

    [Export(PropertyHint.ColorNoAlpha)] public Color FoamColorValue { get; set; } = new(1.0f, 1.0f, 1.0f);

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float FoamColorAlpha { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.0, 2.0, 0.01")]
    public float FoamDepthThreshold { get; set; } = 0.2f;

    [Export(PropertyHint.Range, "0.01, 1.0, 0.01")]
    public float FoamSoftness { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float WaterGlossiness { get; set; } = 0.95f;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float WaterMetallic { get; set; }

    [Export] public bool FollowPlanetRadius { get; set; } = true;

    public override void _Ready()
    {
        base._Ready();

        var waterMesh = GetNode<MeshInstance3D>(WaterMeshInstanceNodeName);
        if (waterMesh == null)
        {
            GD.PushError($"WaterSphere: Could not find MeshInstance3D node named '{WaterMeshInstanceNodeName}'.");
            _waterMeshInstance = new MeshInstance3D();
            _waterMeshInstance.Name = WaterMeshInstanceNodeName;
            AddChild(_waterMeshInstance);
        }

        FindParentPlanet();
        CreateWaterSphere();

        _parentPlanet?.Connect("radius_changed", Callable.From(OnPlanetRadiusChanged));
    }

    public override void _ExitTree()
    {
        if (_parentPlanet != null && _parentPlanet.IsConnected("radius_changed", Callable.From(OnPlanetRadiusChanged)))
        {
            _parentPlanet.Disconnect("radius_changed", Callable.From(OnPlanetRadiusChanged));
        }

        base._ExitTree();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (!HasNode(WaterMeshInstanceNodeName))
        {
            warnings.Add(
                $"Required node '{WaterMeshInstanceNodeName}' (MeshInstance3D) is missing or has a different name.");
        }

        if (WaterShaderResource == null)
        {
            warnings.Add("The 'Water Shader Resource' property must be assigned a valid Shader resource.");
        }

        Node parent = GetParent();
        bool foundPlanet = false;
        while (parent != null)
        {
            if (parent is Planet)
            {
                foundPlanet = true;
                break;
            }

            if (parent.GetParent() == null) break;
            parent = parent.GetParent();
        }

        if (!foundPlanet)
        {
            warnings.Add(
                "WaterSphere performs best as a child (direct or indirect) of a Planet node for radius synchronization.");
        }


        return warnings.ToArray();
    }

    private void FindParentPlanet()
    {
        var parent = GetParent();
        while (parent != null)
        {
            if (parent is Planet planet)
            {
                _parentPlanet = planet;
                break;
            }

            if (parent.GetParent() == null) break;
            parent = parent.GetParent();
        }

        if (_parentPlanet == null && Engine.IsEditorHint())
        {
            GD.Print(
                "WaterSphere: No parent Planet found during FindParentPlanet. Water radius might not sync automatically if 'Follow Planet Radius' is enabled.");
        }
    }

    public void CreateWaterSphere()
    {
        if (_waterMeshInstance == null) return;

        var radius = CalculateWaterRadius();

        var sphereMesh = new SphereMesh();
        sphereMesh.Radius = radius;
        sphereMesh.Height = radius * 2;
        sphereMesh.RadialSegments = 64;
        sphereMesh.Rings = 32;

        var waterMaterial = CreateWaterMaterial();

        _waterMeshInstance.Mesh = sphereMesh;
        _waterMeshInstance.MaterialOverride = waterMaterial;
    }

    private float CalculateWaterRadius()
    {
        if (_parentPlanet != null && FollowPlanetRadius)
        {
            return _parentPlanet.Radius * WaterLevel;
        }

        var nodeParent = GetParent<Node3D>();
        if (nodeParent != null)
        {
            return nodeParent.Scale.X * WaterLevel;
        }

        return 1.0f * WaterLevel;
    }

    private ShaderMaterial CreateWaterMaterial()
    {
        if (WaterShaderResource == null)
        {
            GD.PushError("WaterSphere: Cannot create material, WaterShaderResource is not set.");
            return null;
        }

        var shaderMaterial = new ShaderMaterial();
        shaderMaterial.Shader = WaterShaderResource;

        SetMaterialParameters(shaderMaterial);

        return shaderMaterial;
    }

    private void SetMaterialParameters(ShaderMaterial material)
    {
        if (material == null) return;

        material.SetShaderParameter("shallow_color", new Color(ShallowColorValue, ShallowColorAlpha));
        material.SetShaderParameter("deep_color", new Color(DeepColorValue, DeepColorAlpha));
        material.SetShaderParameter("depth_distance", DepthDistance);
        material.SetShaderParameter("foam_color", new Color(FoamColorValue, FoamColorAlpha));
        material.SetShaderParameter("foam_depth_threshold", FoamDepthThreshold);
        material.SetShaderParameter("foam_softness", FoamSoftness);
        material.SetShaderParameter("roughness", 1.0f - WaterGlossiness);
        material.SetShaderParameter("metallic", WaterMetallic);
    }


    public void UpdateWaterParameters()
    {
        if (_waterMeshInstance?.MaterialOverride is ShaderMaterial material)
        {
            SetMaterialParameters(material);
        }

        UpdateWaterRadius();
    }

    private void UpdateWaterRadius()
    {
        if (_waterMeshInstance == null) return;
        var radius = CalculateWaterRadius();

        switch (_waterMeshInstance.Mesh)
        {
            case SphereMesh sphereMesh when Mathf.IsEqualApprox(sphereMesh.Radius, radius):
                return;
            case SphereMesh sphereMesh:
                sphereMesh.Radius = radius;
                sphereMesh.Height = radius * 2;
                break;
            case null:
                CreateWaterSphere();
                break;
        }
    }

    private void OnPlanetRadiusChanged()
    {
        if (FollowPlanetRadius)
        {
            UpdateWaterRadius();
        }
    }


    [Signal]
    public delegate void WaterParametersChangedEventHandler();


    public void NotifyPropertyChanged()
    {
        if (!Engine.IsEditorHint()) return;

        UpdateWaterParameters();
        EmitSignal(SignalName.WaterParametersChanged);
    }

    public void SetWaterLevel(float level)
    {
        WaterLevel = Mathf.Clamp(level, 0.0f, 1.0f);
        UpdateWaterRadius();
        if (_waterMeshInstance?.MaterialOverride is ShaderMaterial material)
        {
            SetMaterialParameters(material);
        }

        if (Engine.IsEditorHint())
        {
            NotifyPropertyChanged();
        }
    }
}