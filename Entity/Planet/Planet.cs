using Godot;

[Tool]
public partial class Planet : Node3D
{
	// Mesh Parameters
	[Export] public int Resolution = 32;
	[Export] public float Radius { get; set; } = 1.0f;
	[Export] public float NoiseScale { get; set; } = 1.0f;
	[Export] public int NoiseOctaves { get; set; } = 4;
	[Export] public float NoisePersistence { get; set; } = 0.5f;
	[Export] public float NoiseLacunarity { get; set; } = 2.0f;
	[Export] public Vector3 NoiseOffset { get; set; } = Vector3.Zero;
	[Export] public float HeightScale { get; set; } = 2.0f;
	
	// Mat Parameters
	[Export] public Color BaseColor = Colors.Blue;
	[Export] public Color SecondaryColor = Colors.Green;
	[Export] public float ColorBlend = 0.5f;
	[Export] public float Roughness = 0.8f;
	[Export] public float Metallic = 0.0f;
	
	private FastNoiseLite _noise;
	private ArrayMesh _mesh;
	private SurfaceTool _surfaceTool;
	private ShaderMaterial _material;
	private MeshInstance3D _meshInstance;
	private RandomNumberGenerator _rng;
	
	public override void _Ready()
	{
		GD.Print("Planet._Ready() called");
		_meshInstance = new MeshInstance3D();
		AddChild(_meshInstance);
		
		_rng = new RandomNumberGenerator();
		_rng.Randomize();
		
		SetupNoise();
		CreateMaterial();
		GeneratePlanet();
		
		// for testing
		var timer = GetNodeOrNull<Timer>("Timer");
		if (timer != null)
		{
			GD.Print("Timer found, connecting signal");
			timer.Timeout += OnTimerTimeout;
		}
		else
		{
			GD.PrintErr("Timer not found!");
		}
	}
	
	private void SetupNoise()
	{
		_noise = new FastNoiseLite();
		_noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		_noise.Frequency = NoiseScale;
		_noise.FractalOctaves = NoiseOctaves;
		_noise.FractalLacunarity = NoiseLacunarity;
		_noise.FractalGain = NoisePersistence;
	}
	
	private void RandomizeNoise()
	{
		GD.Print("Randomizing noise parameters");
		NoiseScale = _rng.RandfRange(1.0f, 1.5f);
		NoiseOctaves = _rng.RandiRange(6, 8);
		NoisePersistence = _rng.RandfRange(0.2f, 0.3f);
		NoiseLacunarity = _rng.RandfRange(2.5f, 3.5f);
		NoiseOffset = new Vector3(
			_rng.RandfRange(-1000f, 1000f),
			_rng.RandfRange(-1000f, 1000f),
			_rng.RandfRange(-1000f, 1000f)
		);
		HeightScale = _rng.RandfRange(2.0f, 2.5f);
		
		GD.Print($"New noise parameters: Scale={NoiseScale}, Octaves={NoiseOctaves}, Lacunarity={NoiseLacunarity}, Gain={NoisePersistence}, Offset={NoiseOffset}, HeightScale={HeightScale}");
	}
	
	private void CreateMaterial()
	{
		GD.Print("Creating material");
		_material = new ShaderMaterial();
		_material.Shader = new Shader();
		
		string shaderCode = @"
			shader_type spatial;
			render_mode cull_disabled;
			
			uniform vec4 base_color : source_color = vec4(0.0, 0.0, 1.0, 1.0);
			uniform vec4 secondary_color : source_color = vec4(0.0, 1.0, 0.0, 1.0);
			uniform float color_blend : hint_range(0.0, 1.0) = 0.5;
			uniform float roughness : hint_range(0.0, 1.0) = 0.8;
			uniform float metallic : hint_range(0.0, 1.0) = 0.0;
			uniform float height_scale : hint_range(0.0, 1.0) = 0.1;
			
			void fragment() {
				vec3 normal = NORMAL;
				float height = dot(normal, vec3(0.0, 1.0, 0.0)) * 0.5 + 0.5;
				
				vec4 color = mix(base_color, secondary_color, height * color_blend);
				ALBEDO = color.rgb;
				ROUGHNESS = roughness;
				METALLIC = metallic;
			}
		";
		
		_material.Shader.Code = shaderCode;
		UpdateMaterialParameters();
	}
	
	public void GeneratePlanet()
	{
		GD.Print("Generating planet mesh");
		_surfaceTool = new SurfaceTool();
		_surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
		
		//  vertices
		for (int i = 0; i <= Resolution; i++)
		{
			float lat = Mathf.Pi * (-0.5f + (float)i / Resolution);
			float sinLat = Mathf.Sin(lat);
			float cosLat = Mathf.Cos(lat);
			
			for (int j = 0; j <= Resolution; j++)
			{
				float lon = 2 * Mathf.Pi * (float)j / Resolution;
				float sinLon = Mathf.Sin(lon);
				float cosLon = Mathf.Cos(lon);
				
				Vector3 normal = new Vector3(cosLon * cosLat, sinLat, sinLon * cosLat);
				float height = GetNoiseHeight(normal);
				Vector3 vertex = normal * (Radius + height);
				
				_surfaceTool.SetNormal(normal);
				_surfaceTool.SetUV(new Vector2((float)j / Resolution, (float)i / Resolution));
				_surfaceTool.AddVertex(vertex);
			}
		}
		
		// Generate indices
		for (int i = 0; i < Resolution; i++)
		{
			for (int j = 0; j < Resolution; j++)
			{
				int topLeft = i * (Resolution + 1) + j;
				int topRight = topLeft + 1;
				int bottomLeft = (i + 1) * (Resolution + 1) + j;
				int bottomRight = bottomLeft + 1;
				
				_surfaceTool.AddIndex(topLeft);
				_surfaceTool.AddIndex(bottomLeft);
				_surfaceTool.AddIndex(topRight);
				
				_surfaceTool.AddIndex(topRight);
				_surfaceTool.AddIndex(bottomLeft);
				_surfaceTool.AddIndex(bottomRight);
			}
		}
		
		_surfaceTool.GenerateNormals();
		_mesh = _surfaceTool.Commit();
		_meshInstance.Mesh = _mesh;
		_meshInstance.MaterialOverride = _material;
		GD.Print($"Planet generated");
	}
	
	private float GetNoiseHeight(Vector3 normal)
	{
		Vector3 point = normal + NoiseOffset;
		float height = _noise.GetNoise3Dv(point) * HeightScale;
		return height;
	}
	
	private void UpdateMaterialParameters()
	{
		if (_material != null)
		{
			GD.Print("Updating material parameters");
			_material.SetShaderParameter("base_color", BaseColor);
			_material.SetShaderParameter("secondary_color", SecondaryColor);
			_material.SetShaderParameter("color_blend", ColorBlend);
			_material.SetShaderParameter("roughness", Roughness);
			_material.SetShaderParameter("metallic", Metallic);
			_material.SetShaderParameter("height_scale", HeightScale);
		}
		else
		{
			GD.PrintErr("Material is null");
		}
	}
	
	public void Regenerate()
	{
		GD.Print("Regenerating planet");
		RandomizeNoise();
		SetupNoise();
		UpdateMaterialParameters();
		GeneratePlanet();
	}
	
	private void OnTimerTimeout()
	{
		// for testing
		GD.Print("Regenerating planet");
		Regenerate();
	}
} 
