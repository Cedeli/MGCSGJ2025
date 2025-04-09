// File: Entity/Planet/Planet.cs
using System;
using Godot;

[Tool]
public partial class Planet : CelestialBody
{
	[Export]
	public int Resolution = 32;

	[Export]
	public float NoiseScale { get; set; } = 1.0f;

	[Export]
	public int NoiseOctaves { get; set; } = 4;

	[Export]
	public float NoisePersistence { get; set; } = 0.5f;

	[Export]
	public float NoiseLacunarity { get; set; } = 2.0f;

	[Export]
	public Vector3 NoiseOffset { get; set; } = Vector3.Zero;

	[Export]
	public float HeightScale { get; set; } = 0.1f;

	[Export]
	public Color BaseColor = new Color("2a6f97");

	[Export]
	public Color SecondaryColor = new Color("6f7f69");

	[Export]
	public float ColorBlend = 0.5f;

	[Export]
	public float Roughness = 0.85f;

	[Export]
	public float Metallic = 0.05f;

	[Export(PropertyHint.Range, "1.0, 100.0, 0.5")]
	public float DetailNoiseScale = 30.0f;

	[Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
	public float DetailNoiseIntensity = 0.15f;

	[Export]
	public bool HasAtmosphere = true;

	[Export]
	public Color AtmosphereColor = new Color("a9d6e5");

	[Export(PropertyHint.Range, "0.01, 0.5, 0.01")]
	public float AtmosphereThickness = 0.05f;

	[Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
	public float AtmosphereDensity = 1.0f;

	[Export(PropertyHint.Range, "0.1, 10.0, 0.1")]
	public float AtmosphereFalloff = 5.0f;

	[Export]
	public bool HasWater = true;

	[Export(PropertyHint.Range, "-1.0, 1.0, 0.01")]
	public float WaterLevelOffsetFactor = -0.3f;

	[Export]
	public Color WaterColor = new Color("60a3bc");

	[Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
	public float WaterSpecular = 0.5f;

	[Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
	public float WaterRoughness = 0.1f;

	[Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
	public float WaterTransparency = 0.7f;

	private FastNoiseLite _noise;
	private ArrayMesh _mesh;
	private SurfaceTool _surfaceTool;
	private ShaderMaterial _material;
	private MeshInstance3D _planetVisualMeshInstance;
	private RandomNumberGenerator _rng;

	private MeshInstance3D _atmosphereInstance;
	private ShaderMaterial _atmosphereMaterial;
	private SphereMesh _atmosphereMesh;

	private MeshInstance3D _waterInstance;
	private StandardMaterial3D _waterMaterial;
	private SphereMesh _waterMesh;
	private float _actualWaterLevelOffset = 0f;

	private bool _isReady = false;

	public override void _Ready()
	{
		base._Ready();

		_planetVisualMeshInstance = GetNodeOrNull<MeshInstance3D>("PlanetVisualMeshInstance");
		if (_planetVisualMeshInstance == null)
		{
			_planetVisualMeshInstance = new MeshInstance3D { Name = "PlanetVisualMeshInstance" };
			AddChild(_planetVisualMeshInstance);
			if (Engine.IsEditorHint() && GetTree() != null && GetTree().EditedSceneRoot != null)
				_planetVisualMeshInstance.Owner = GetTree().EditedSceneRoot;
		}

		_atmosphereInstance = GetNodeOrNull<MeshInstance3D>("AtmosphereInstance");
		_waterInstance = GetNodeOrNull<MeshInstance3D>("WaterInstance");

		if (_rng == null)
		{
			_rng = new RandomNumberGenerator();
			_rng.Randomize();
		}

		SetupNoise();
		CreateMaterial();
		CreateAtmosphereMaterial();
		CreateWaterMaterial();

		if (!Engine.IsEditorHint())
		{
			GeneratePlanet();
		}
		else
		{
			UpdateShapeAndMesh();
			UpdateMaterialParameters();
			UpdateAtmosphereMaterialParameters();
			UpdateWaterMaterialParameters();
			if (_planetVisualMeshInstance != null && _material != null)
			{
				_planetVisualMeshInstance.MaterialOverride = _material;
			}
		}
		_isReady = true;

		Regenerate();
	}

	protected override void UpdateShapeAndMesh()
	{
		if (_collisionShape?.Shape is SphereShape3D sphereShape)
		{
			sphereShape.Radius = Radius;
		}
	}

	private void SetupNoise()
	{
		if (_rng == null)
			_rng = new RandomNumberGenerator { Seed = (ulong)GD.Randi() };

		_noise = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
			Frequency = NoiseScale,
			FractalOctaves = NoiseOctaves,
			FractalLacunarity = NoiseLacunarity,
			FractalGain = NoisePersistence,
			Seed = (int)_rng.Randi(),
		};
	}

	private void RandomizeNoise()
	{
		if (_rng == null)
			_rng = new RandomNumberGenerator { Seed = (ulong)GD.Randi() };

		NoiseScale = _rng.RandfRange(0.8f, 2.0f);
		NoiseOctaves = _rng.RandiRange(5, 9);
		NoisePersistence = _rng.RandfRange(0.3f, 0.5f);
		NoiseLacunarity = _rng.RandfRange(2.0f, 3.0f);
		NoiseOffset = new Vector3(
			_rng.RandfRange(-1000f, 1000f),
			_rng.RandfRange(-1000f, 1000f),
			_rng.RandfRange(-1000f, 1000f)
		);
		HeightScale = _rng.RandfRange(0.03f, 0.12f) * Radius;

		if (_noise != null)
			_noise.Seed = (int)_rng.Randi();

		WaterLevelOffsetFactor = _rng.RandfRange(-0.7f, -0.2f);
		_actualWaterLevelOffset = WaterLevelOffsetFactor * HeightScale;
	}

	private void RandomizeColors()
	{
		if (_rng == null)
			_rng = new RandomNumberGenerator { Seed = (ulong)GD.Randi() };

		float baseHue = _rng.RandfRange(0.45f, 0.65f);
		float baseSat = _rng.RandfRange(0.3f, 0.7f);
		float baseVal = _rng.RandfRange(0.2f, 0.6f);
		BaseColor = Color.FromHsv(baseHue, baseSat, baseVal);

		float secHue,
			secSat,
			secVal,
			choice = _rng.Randf();
		if (choice < 0.4f)
		{
			secHue = _rng.RandfRange(0.05f, 0.15f);
			secSat = _rng.RandfRange(0.3f, 0.7f);
			secVal = _rng.RandfRange(0.3f, 0.7f);
		}
		else if (choice < 0.7f)
		{
			secHue = _rng.RandfRange(0.2f, 0.4f);
			secSat = _rng.RandfRange(0.2f, 0.6f);
			secVal = _rng.RandfRange(0.3f, 0.6f);
		}
		else
		{
			secHue = _rng.Randf();
			secSat = _rng.RandfRange(0.0f, 0.15f);
			secVal = _rng.RandfRange(0.4f, 0.8f);
		}
		SecondaryColor = Color.FromHsv(secHue, secSat, secVal);
		ColorBlend = _rng.RandfRange(0.2f, 0.6f);

		float atmHue = _rng.RandfRange(0.55f, 0.68f);
		float atmSat = _rng.RandfRange(0.1f, 0.4f);
		float atmVal = _rng.RandfRange(0.65f, 0.9f);
		AtmosphereColor = Color.FromHsv(atmHue, atmSat, atmVal);
		AtmosphereDensity = _rng.RandfRange(0.5f, 2.5f);
		AtmosphereFalloff = _rng.RandfRange(3.0f, 7.0f);
		AtmosphereThickness = _rng.RandfRange(0.03f, 0.15f);

		float waterHue = _rng.RandfRange(0.5f, 0.65f);
		float waterSat = _rng.RandfRange(0.4f, 0.8f);
		float waterVal = _rng.RandfRange(0.3f, 0.7f);
		WaterColor = Color.FromHsv(waterHue, waterSat, waterVal);
		WaterRoughness = _rng.RandfRange(0.05f, 0.25f);
		WaterSpecular = _rng.RandfRange(0.4f, 0.8f);
		WaterTransparency = _rng.RandfRange(0.6f, 0.85f);

		DetailNoiseScale = _rng.RandfRange(15.0f, 50.0f);
		DetailNoiseIntensity = _rng.RandfRange(0.08f, 0.25f);
	}

	private void CreateMaterial()
	{
		if (_material == null)
		{
			_material = new ShaderMaterial();
			_material.Shader = new Shader();

			string shaderCode =
				@"
				shader_type spatial;
				render_mode blend_mix, depth_draw_opaque, cull_back;

				uniform vec4 base_color : source_color = vec4(0.0, 0.0, 1.0, 1.0);
				uniform vec4 secondary_color : source_color = vec4(0.0, 1.0, 0.0, 1.0);
				uniform float color_blend : hint_range(0.0, 1.0) = 0.5;
				uniform float roughness : hint_range(0.0, 1.0) = 0.8;
				uniform float metallic : hint_range(0.0, 1.0) = 0.0;
				uniform float detail_noise_scale : hint_range(1.0, 100.0) = 20.0;
				uniform float detail_noise_intensity : hint_range(0.0, 1.0) = 0.15;

				varying float v_height_normalized;

				float hash31(vec3 p3) {
					p3 = fract(p3 * vec3(.1031, .1030, .0973));
					p3 += dot(p3, p3.yzx + 33.33);
					return fract((p3.x + p3.y) * p3.z);
				}

				void vertex() {
					v_height_normalized = COLOR.r;
				}

				void fragment() {
					float blend_factor = pow(v_height_normalized, 1.5);
					blend_factor *= color_blend;
					blend_factor = clamp(blend_factor, 0.0, 1.0);
					vec4 color = mix(base_color, secondary_color, blend_factor);

					vec3 world_pos_scaled = VERTEX * detail_noise_scale;
					float noise_value = hash31(world_pos_scaled);
					float noise_darken_factor = mix(1.0 - detail_noise_intensity, 1.0, noise_value);

					ALBEDO = color.rgb * noise_darken_factor;
					ROUGHNESS = roughness;
					METALLIC = metallic;
				}
			";

			_material.Shader.Code = shaderCode;
		}
		UpdateMaterialParameters();
	}

	private void CreateAtmosphereMaterial()
	{
		if (_atmosphereMaterial == null)
		{
			_atmosphereMaterial = new ShaderMaterial();
			_atmosphereMaterial.Shader = new Shader();

			string shaderCode =
				@"
				shader_type spatial;
				render_mode blend_add, depth_draw_never, cull_front, unshaded;

				uniform vec4 atmosphere_color : source_color = vec4(0.3, 0.5, 1.0, 1.0);
				uniform float density : hint_range(0.1, 10.0) = 1.0;
				uniform float falloff : hint_range(0.1, 10.0) = 4.0;

				void vertex() { }

				void fragment() {
					float fresnel = pow(1.0 - abs(dot(normalize(VERTEX - CAMERA_POSITION_WORLD), normalize(NORMAL))), falloff);
					ALBEDO = atmosphere_color.rgb;
					ALPHA = fresnel * density * atmosphere_color.a;
				}
			";
			_atmosphereMaterial.Shader.Code = shaderCode;
		}
		UpdateAtmosphereMaterialParameters();
	}

	private void CreateWaterMaterial()
	{
		if (_waterMaterial == null)
		{
			_waterMaterial = new StandardMaterial3D();
			_waterMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			_waterMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;
			_waterMaterial.CullMode = BaseMaterial3D.CullModeEnum.Back;
		}
		UpdateWaterMaterialParameters();
	}

	private void UpdateAtmosphereMaterialParameters()
	{
		if (_atmosphereMaterial != null)
		{
			_atmosphereMaterial.SetShaderParameter("atmosphere_color", AtmosphereColor);
			_atmosphereMaterial.SetShaderParameter("density", AtmosphereDensity);
			_atmosphereMaterial.SetShaderParameter("falloff", AtmosphereFalloff);
		}
	}

	private void UpdateWaterMaterialParameters()
	{
		if (_waterMaterial == null)
			return;

		Color transparentColor = WaterColor;
		transparentColor.A = WaterTransparency;
		_waterMaterial.AlbedoColor = transparentColor;
		_waterMaterial.Roughness = WaterRoughness;
		_waterMaterial.Metallic = 0.0f;
	}

	public void GeneratePlanet()
	{
		if (_planetVisualMeshInstance == null)
			return;
		if (_noise == null)
			SetupNoise();

		_surfaceTool = new SurfaceTool();
		_surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
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
				Vector3 baseNormal = new Vector3(
					cosLon * cosLat,
					sinLat,
					sinLon * cosLat
				).Normalized();
				float height = GetNoiseHeight(baseNormal);
				Vector3 vertex = baseNormal * (Radius + height);
				float maxPossibleHeight = HeightScale;
				float minPossibleHeight = -HeightScale;
				float normalizedHeight = Mathf.InverseLerp(
					minPossibleHeight,
					maxPossibleHeight,
					height
				);
				normalizedHeight = Mathf.Clamp(normalizedHeight, 0.0f, 1.0f);
				_surfaceTool.SetColor(new Color(normalizedHeight, 0, 0, 1));
				_surfaceTool.SetNormal(baseNormal);
				_surfaceTool.SetUV(new Vector2((float)j / Resolution, (float)i / Resolution));
				_surfaceTool.AddVertex(vertex);
			}
		}
		for (int i = 0; i < Resolution; i++)
		{
			for (int j = 0; j < Resolution; j++)
			{
				int topLeft = i * (Resolution + 1) + j;
				int topRight = topLeft + 1;
				int bottomLeft = (i + 1) * (Resolution + 1) + j;
				int bottomRight = bottomLeft + 1;
				_surfaceTool.AddIndex(topLeft);
				_surfaceTool.AddIndex(topRight);
				_surfaceTool.AddIndex(bottomLeft);
				_surfaceTool.AddIndex(topRight);
				_surfaceTool.AddIndex(bottomRight);
				_surfaceTool.AddIndex(bottomLeft);
			}
		}
		_surfaceTool.GenerateNormals();
		_mesh = _surfaceTool.Commit();

		_planetVisualMeshInstance.Mesh = _mesh;

		if (_material != null)
		{
			_planetVisualMeshInstance.MaterialOverride = _material;
		}

		UpdateShapeAndMesh();

		GenerateOrUpdateAtmosphereMesh();
		GenerateOrUpdateWaterMesh();
	}

	private void GenerateOrUpdateAtmosphereMesh()
	{
		if (!HasAtmosphere)
		{
			if (_atmosphereInstance != null)
				_atmosphereInstance.Visible = false;
			return;
		}
		if (_atmosphereInstance == null)
		{
			_atmosphereMesh = new SphereMesh();
			_atmosphereInstance = new MeshInstance3D
			{
				Name = "AtmosphereInstance",
				Mesh = _atmosphereMesh,
				MaterialOverride = _atmosphereMaterial,
				SortingOffset = 1.0f,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			AddChild(_atmosphereInstance);
			if (Engine.IsEditorHint() && GetTree() != null && GetTree().EditedSceneRoot != null)
				_atmosphereInstance.Owner = GetTree().EditedSceneRoot;
		}
		_atmosphereInstance.Visible = true;
		float atmosphereRadius = Radius + (Radius * AtmosphereThickness);
		if (_atmosphereMesh != null)
		{
			_atmosphereMesh.Radius = atmosphereRadius;
			_atmosphereMesh.Height = atmosphereRadius * 2.0f;
		}
		UpdateAtmosphereMaterialParameters();
	}

	private void GenerateOrUpdateWaterMesh()
	{
		if (!HasWater)
		{
			if (_waterInstance != null)
				_waterInstance.Visible = false;
			return;
		}

		if (_waterInstance == null)
		{
			_waterMesh = new SphereMesh();
			_waterInstance = new MeshInstance3D
			{
				Name = "WaterInstance",
				Mesh = _waterMesh,
				MaterialOverride = _waterMaterial,
				SortingOffset = 0.5f,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			AddChild(_waterInstance);
			if (Engine.IsEditorHint() && GetTree() != null && GetTree().EditedSceneRoot != null)
				_waterInstance.Owner = GetTree().EditedSceneRoot;
		}

		_waterInstance.Visible = true;

		float waterSphereRadius = Radius + _actualWaterLevelOffset;
		waterSphereRadius = Mathf.Max(0.01f, waterSphereRadius);

		if (_waterMesh != null)
		{
			_waterMesh.Radius = waterSphereRadius;
			_waterMesh.Height = waterSphereRadius * 2.0f;
		}
		UpdateWaterMaterialParameters();
	}

	private float GetNoiseHeight(Vector3 baseNormal)
	{
		if (_noise == null)
		{
			SetupNoise();
			if (_noise == null)
				return 0f;
		}
		Vector3 point = baseNormal + NoiseOffset;
		float rawNoise = _noise.GetNoise3Dv(point);
		float height = rawNoise * HeightScale;
		return height;
	}

	private void UpdateMaterialParameters()
	{
		if (_material != null)
		{
			_material.SetShaderParameter("base_color", BaseColor);
			_material.SetShaderParameter("secondary_color", SecondaryColor);
			_material.SetShaderParameter("color_blend", ColorBlend);
			_material.SetShaderParameter("roughness", Roughness);
			_material.SetShaderParameter("metallic", Metallic);
			_material.SetShaderParameter("detail_noise_scale", DetailNoiseScale);
			_material.SetShaderParameter("detail_noise_intensity", DetailNoiseIntensity);
		}
		else
			GD.PrintErr("Cannot update parameters as material is null");
	}

	public void Regenerate()
	{
		if (!_isReady)
			return;

		RandomizeNoise();
		RandomizeColors();
		SetupNoise();

		UpdateMaterialParameters();
		UpdateAtmosphereMaterialParameters();
		UpdateWaterMaterialParameters();

		if (!Engine.IsEditorHint())
		{
			GeneratePlanet();
		}
		else
		{
			UpdateShapeAndMesh();
			GenerateOrUpdateAtmosphereMesh();
			GenerateOrUpdateWaterMesh();
		}

		if (!Engine.IsEditorHint())
		{
			if (SurfaceGravity > 0.001f && GravitationalConstant > 0.001f && Radius > 0.01f)
			{
				Mass = SurfaceGravity * (Radius * Radius) / GravitationalConstant;
			}
			else
			{
				Mass = InitialMass;
			}
			GD.Print($"{Name}: Regenerated. New Mass: {Mass}");
			if (AutoCalculateOrbitalVelocity && OrbitParent != null)
			{
				OrbitalVelocity();
			}
		}
	}

	public Vector3 GetSurfacePosition(Vector2 uv)
	{
		float lon = uv.X * 2.0f * Mathf.Pi;
		float lat = Mathf.Pi * (-0.5f + uv.Y);
		float cosLat = Mathf.Cos(lat);
		Vector3 normal = new Vector3(
			Mathf.Cos(lon) * cosLat,
			Mathf.Sin(lat),
			Mathf.Sin(lon) * cosLat
		).Normalized();
		float height = GetNoiseHeight(normal);
		Vector3 position = normal * (Radius + height);
		return position;
	}
}
