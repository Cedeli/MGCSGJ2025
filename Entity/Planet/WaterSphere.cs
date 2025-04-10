using Godot;
using System;

[Tool]
public partial class WaterSphere : Node3D
{
    #region Nodes

    private MeshInstance3D _waterMeshInstance;
    private Planet _parentPlanet;

    private const string WaterMeshInstanceNodeName = "WaterMeshInstance3D";

    #endregion

    #region Exports

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float WaterLevel { get; set; } = 0.98f;

    [Export]
    public Color WaterColor { get; set; } = new Color(0.05f, 0.3f, 0.5f, 0.8f);

    [Export(PropertyHint.Range, "0.0, 5.0, 0.1")]
    public float FresnelPower { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float WaterTransparency { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float WaterGlossiness { get; set; } = 0.9f;

    [Export(PropertyHint.Range, "0.0, 2.0, 0.01")]
    public float WaveHeight { get; set; } = 0.02f;

    [Export(PropertyHint.Range, "0.1, 10.0, 0.1")]
    public float WaveSpeed { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.1, 20.0, 0.1")]
    public float WaveScale { get; set; } = 5.0f;

    [Export]
    public bool FollowPlanetRadius { get; set; } = true;

    #endregion

    #region Godot Lifecycle Methods

    public override void _Ready()
    {
        base._Ready();
        
        FindParentPlanet();
        CreateWaterSphere();
        
        if (_parentPlanet != null)
        {
            _parentPlanet.Connect("radius_changed", Callable.From(OnPlanetRadiusChanged));
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdateShaderTime((float)delta);
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
        var warnings = new System.Collections.Generic.List<string>();
        
        if (_waterMeshInstance == null)
        {
            warnings.Add($"Required node '{WaterMeshInstanceNodeName}' (MeshInstance3D) is missing.");
        }

        if (_parentPlanet == null)
        {
            warnings.Add("WaterSphere should be a child of a Planet node.");
        }

        return warnings.ToArray();
    }

    #endregion

    #region Initialization Methods

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
        
        if (_parentPlanet == null)
        {
            GD.PushWarning("WaterSphere: No parent Planet found. Water radius will not adjust automatically.");
        }
    }

    #endregion

    #region Water Sphere Creation

    public void CreateWaterSphere()
    {
        float radius = CalculateWaterRadius();
        
        // Create a sphere mesh for water
        var sphereMesh = new SphereMesh();
        sphereMesh.Radius = radius;
        sphereMesh.Height = radius * 2;
        sphereMesh.RadialSegments = 64;
        sphereMesh.Rings = 32;
        
        // Create the water material with fresnel effect
        var waterMaterial = CreateWaterMaterial();
        
        // Apply to the mesh instance
        _waterMeshInstance.Mesh = sphereMesh;
        _waterMeshInstance.MaterialOverride = waterMaterial;
        
        GD.Print($"WaterSphere: Created water sphere with radius {radius}");
    }

    private float CalculateWaterRadius()
    {
        if (_parentPlanet != null && FollowPlanetRadius)
        {
            return _parentPlanet.Radius * WaterLevel;
        }
        
        // Default radius if no planet is found
        return 10.0f * WaterLevel;
    }

    private ShaderMaterial CreateWaterMaterial()
    {
        var shaderMaterial = new ShaderMaterial();
        shaderMaterial.Shader = CreateWaterShader();
        
        // Set initial parameters
        shaderMaterial.SetShaderParameter("water_color", WaterColor);
        shaderMaterial.SetShaderParameter("fresnel_power", FresnelPower);
        shaderMaterial.SetShaderParameter("transparency", WaterTransparency);
        shaderMaterial.SetShaderParameter("glossiness", WaterGlossiness);
        shaderMaterial.SetShaderParameter("wave_height", WaveHeight);
        shaderMaterial.SetShaderParameter("wave_speed", WaveSpeed);
        shaderMaterial.SetShaderParameter("wave_scale", WaveScale);
        shaderMaterial.SetShaderParameter("time", 0.0f);
        
        return shaderMaterial;
    }

    private Shader CreateWaterShader()
    {
        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode blend_mix, depth_prepass_alpha, diffuse_lambert, specular_schlick_ggx;

uniform vec4 water_color : source_color = vec4(0.05, 0.3, 0.5, 0.8);
uniform float fresnel_power : hint_range(0.0, 5.0) = 2.0;
uniform float transparency : hint_range(0.0, 1.0) = 0.7;
uniform float glossiness : hint_range(0.0, 1.0) = 0.9;
uniform float wave_height : hint_range(0.0, 2.0) = 0.02;
uniform float wave_speed : hint_range(0.1, 10.0) = 1.0;
uniform float wave_scale : hint_range(0.1, 20.0) = 5.0;
uniform float time = 0.0;

varying vec3 vertex_pos;
varying vec3 normal_interp;

void vertex() {
    vertex_pos = VERTEX;
    
    // Add some gentle wave displacement
    float wave = sin(VERTEX.x * wave_scale + time * wave_speed) * 
                cos(VERTEX.z * wave_scale + time * wave_speed * 0.8) * 
                sin(VERTEX.y * wave_scale + time * wave_speed * 1.2);
    
    VERTEX += NORMAL * wave * wave_height;
    normal_interp = NORMAL;
}

void fragment() {
    // Basic water color
    ALBEDO = water_color.rgb;
    
    // Calculate fresnel effect
    vec3 view_dir = normalize(CAMERA_POSITION_WORLD - VERTEX);
    float fresnel = pow(1.0 - dot(NORMAL, view_dir), fresnel_power);
    
    // Apply fresnel to alpha and reflection
    ALPHA = min(1.0, water_color.a + fresnel * 0.5);
    
    // Water properties
    METALLIC = 0.1;
    ROUGHNESS = 1.0 - glossiness;
    SPECULAR = 0.5 + fresnel * 0.2;
    
    // Add refraction
    REFRACTION = 0.05 * (1.0 - fresnel);
    
    // Subsurface scattering effect
    SSS_STRENGTH = 0.5;
    
    // Control transparency
    ALPHA *= transparency;
}
";
        return shader;
    }

    #endregion

    #region Update Methods

    private void UpdateShaderTime(float delta)
    {
        if (_waterMeshInstance?.MaterialOverride is ShaderMaterial material)
        {
            float currentTime = material.GetShaderParameter("time").As<float>();
            float newTime = currentTime + delta;
            material.SetShaderParameter("time", newTime);
        }
    }

    public void UpdateWaterParameters()
    {
        if (_waterMeshInstance?.MaterialOverride is ShaderMaterial material)
        {
            material.SetShaderParameter("water_color", WaterColor);
            material.SetShaderParameter("fresnel_power", FresnelPower);
            material.SetShaderParameter("transparency", WaterTransparency);
            material.SetShaderParameter("glossiness", WaterGlossiness);
            material.SetShaderParameter("wave_height", WaveHeight);
            material.SetShaderParameter("wave_speed", WaveSpeed);
            material.SetShaderParameter("wave_scale", WaveScale);
        }
        
        UpdateWaterRadius();
    }

    private void UpdateWaterRadius()
    {
        float radius = CalculateWaterRadius();
        
        if (_waterMeshInstance?.Mesh is SphereMesh sphereMesh)
        {
            sphereMesh.Radius = radius;
            sphereMesh.Height = radius * 2;
        }
    }

    private void OnPlanetRadiusChanged()
    {
        if (FollowPlanetRadius)
        {
            UpdateWaterRadius();
        }
    }

    #endregion

    #region Public Methods

    [Signal]
    public delegate void WaterParametersChangedEventHandler();

    // Method to be called when properties change in the editor
    public void NotifyPropertyChanged()
    {
        UpdateWaterParameters();
        EmitSignal(SignalName.WaterParametersChanged);
    }

    // Public method to update water level and recalculate radius
    public void SetWaterLevel(float level)
    {
        WaterLevel = Mathf.Clamp(level, 0.0f, 1.0f);
        UpdateWaterRadius();
        NotifyPropertyChanged();
    }

    #endregion
}