using Godot;
using System;
using System.Linq;
using System.Runtime.InteropServices;

[Tool]
[GlobalClass]
public partial class PlanetHeight : Resource
{
    #region Signals

    [Signal]
    public delegate void ParametersChangedEventHandler();

    #endregion

    #region Exports

    private string _shaderPath = "";

    [Export(PropertyHint.File, "*.glsl")]
    public string ShaderPath
    {
        get => _shaderPath;
        set
        {
            if (_shaderPath == value) return;
            _shaderPath = value;
            CleanupComputeResources();
            EmitSignal(SignalName.ParametersChanged);
        }
    }

    private float _oceanDepthMultiplier = 1.0f;

    [Export(PropertyHint.Range, "0.1, 10.0, 0.1")]
    public float OceanDepthMultiplier
    {
        get => _oceanDepthMultiplier;
        set
        {
            if (Mathf.IsEqualApprox(_oceanDepthMultiplier, value)) return;
            _oceanDepthMultiplier = value;
            EmitSignal(SignalName.ParametersChanged);
        }
    }

    private float _oceanFloorDepth = 0.5f;

    [Export(PropertyHint.Range, "0.0, 2.0, 0.01")]
    public float OceanFloorDepth
    {
        get => _oceanFloorDepth;
        set
        {
            if (Mathf.IsEqualApprox(_oceanFloorDepth, value)) return;
            _oceanFloorDepth = value;
            EmitSignal(SignalName.ParametersChanged);
        }
    }

    private float _oceanFloorSmoothing = 0.1f;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float OceanFloorSmoothing
    {
        get => _oceanFloorSmoothing;
        set
        {
            if (Mathf.IsEqualApprox(_oceanFloorSmoothing, value)) return;
            _oceanFloorSmoothing = value;
            EmitSignal(SignalName.ParametersChanged);
        }
    }

    private float _mountainBlend = 0.5f;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float MountainBlend
    {
        get => _mountainBlend;
        set
        {
            if (Mathf.IsEqualApprox(_mountainBlend, value)) return;
            _mountainBlend = value;
            EmitSignal(SignalName.ParametersChanged);
        }
    }

    private SimplexNoiseSettings _continentsNoiseSettings;

    [Export]
    public SimplexNoiseSettings ContinentsNoiseSettings
    {
        get => _continentsNoiseSettings;
        set
        {
            if (_continentsNoiseSettings == value) return;
            if (_continentsNoiseSettings != null &&
                _continentsNoiseSettings.IsConnected(Resource.SignalName.Changed, Callable.From(OnSettingsChanged)))
            {
                _continentsNoiseSettings.Disconnect(Resource.SignalName.Changed, Callable.From(OnSettingsChanged));
            }

            _continentsNoiseSettings = value;
            _continentsNoiseSettings?.Connect(Resource.SignalName.Changed, Callable.From(OnSettingsChanged),
                (uint)ConnectFlags.ReferenceCounted);
            EmitSignal(SignalName.ParametersChanged);
        }
    }

    private SimplexNoiseSettings _maskNoiseSettings;

    [Export]
    public SimplexNoiseSettings MaskNoiseSettings
    {
        get => _maskNoiseSettings;
        set
        {
            if (_maskNoiseSettings == value) return;
            if (_maskNoiseSettings != null &&
                _maskNoiseSettings.IsConnected(Resource.SignalName.Changed, Callable.From(OnSettingsChanged)))
            {
                _maskNoiseSettings.Disconnect(Resource.SignalName.Changed, Callable.From(OnSettingsChanged));
            }

            _maskNoiseSettings = value;
            _maskNoiseSettings?.Connect(Resource.SignalName.Changed, Callable.From(OnSettingsChanged),
                (uint)ConnectFlags.ReferenceCounted);
            EmitSignal(SignalName.ParametersChanged);
        }
    }

    private RidgeNoiseSettings _mountainsNoiseSettings;

    [Export]
    public RidgeNoiseSettings MountainsNoiseSettings
    {
        get => _mountainsNoiseSettings;
        set
        {
            if (_mountainsNoiseSettings == value) return;
            if (_mountainsNoiseSettings != null &&
                _mountainsNoiseSettings.IsConnected(Resource.SignalName.Changed, Callable.From(OnSettingsChanged)))
            {
                _mountainsNoiseSettings.Disconnect(Resource.SignalName.Changed, Callable.From(OnSettingsChanged));
            }

            _mountainsNoiseSettings = value;
            _mountainsNoiseSettings?.Connect(Resource.SignalName.Changed, Callable.From(OnSettingsChanged),
                (uint)ConnectFlags.ReferenceCounted);
            EmitSignal(SignalName.ParametersChanged);
        }
    }

    #endregion

    #region Compute State

    private RenderingDevice _rd;
    private Rid _shaderRid;
    private Rid _pipelineRid;
    private bool _isInitialized;

    [StructLayout(LayoutKind.Sequential)]
    private struct ComputeParams
    {
        public float numVertices;
        public float oceanDepthMultiplier;
        public float oceanFloorDepth;
        public float oceanFloorSmoothing;
        public float mountainBlend;
    }

    #endregion

    #region Lifecycle & Signal Handling

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        CleanupComputeResources();
        ContinentsNoiseSettings = null;
        MaskNoiseSettings = null;
        MountainsNoiseSettings = null;
    }

    private void OnSettingsChanged()
    {
        EmitSignal(SignalName.ParametersChanged);
    }

    #endregion

    #region Initialization and Cleanup

    private bool InitializeComputeResources()
    {
        if (_isInitialized) return true;
        if (string.IsNullOrEmpty(_shaderPath))
        {
            GD.PushError($"{nameof(PlanetHeight)} '{ResourceName}': ShaderPath is not set.");
            return false;
        }

        CleanupComputeResources();

        _rd = RenderingServer.CreateLocalRenderingDevice();

        try
        {
            var shaderFile = GD.Load<RDShaderFile>(_shaderPath);
            var shaderBytecode = shaderFile.GetSpirV();

            _shaderRid = _rd.ShaderCreateFromSpirV(shaderBytecode);
            _pipelineRid = _rd.ComputePipelineCreate(_shaderRid);

            _isInitialized = true;
            return true;
        }
        catch (Exception e)
        {
            GD.PushError(
                $"{nameof(PlanetHeight)} '{ResourceName}': Error initializing compute resources: {e.Message}\n{e.StackTrace}");
            CleanupComputeResources();
            _isInitialized = false;
            return false;
        }
    }

    private void CleanupComputeResources()
    {
        if (_rd == null) return;

        GD.Print($"{nameof(PlanetHeight)} '{ResourceName}': Cleaning up compute resources.");

        if (_pipelineRid.IsValid) _rd.FreeRid(_pipelineRid);
        if (_shaderRid.IsValid) _rd.FreeRid(_shaderRid);

        _pipelineRid = default;
        _shaderRid = default;

        _rd.Free();
        _rd = null;

        _isInitialized = false;
    }

    #endregion

    #region Compute Shader

    public float[] Execute(Vector3[] inputUnitVertices, RandomNumberGenerator rng = null)
    {
        if (inputUnitVertices == null || inputUnitVertices.Length == 0)
        {
            GD.PushWarning($"{nameof(PlanetHeight)} '{ResourceName}': Execute called with null or empty vertices.");
            return null;
        }

        if (ContinentsNoiseSettings == null || MaskNoiseSettings == null || MountainsNoiseSettings == null)
        {
            GD.PushError(
                $"{nameof(PlanetHeight)} '{ResourceName}': Execute called but one or more NoiseSettings resources are not assigned.");
            return null;
        }

        if (!_isInitialized && !InitializeComputeResources())
        {
            GD.PushError(
                $"{nameof(PlanetHeight)} '{ResourceName}': Cannot Execute, compute resources failed to initialize.");
            return null;
        }

        if (_rd == null || !_pipelineRid.IsValid)
        {
            GD.PushError($"{nameof(PlanetHeight)} '{ResourceName}': Cannot Execute, compute resources are not valid.");
            CleanupComputeResources();
            return null;
        }

        rng ??= new RandomNumberGenerator();

        var vertexCount = inputUnitVertices.Length;
        Rid vertexBuffer = default;
        Rid heightBuffer = default;
        Rid paramsBuffer = default;
        Rid noiseParamsBuffer = default;
        Rid uniformSet = default;
        float[] outputHeights;

        try
        {
            var vertexFloatData = new float[vertexCount * 3];
            for (var i = 0; i < vertexCount; ++i)
            {
                vertexFloatData[i * 3 + 0] = inputUnitVertices[i].X;
                vertexFloatData[i * 3 + 1] = inputUnitVertices[i].Y;
                vertexFloatData[i * 3 + 2] = inputUnitVertices[i].Z;
            }

            var vertexBytes = MemoryMarshal.AsBytes(vertexFloatData.AsSpan()).ToArray();
            vertexBuffer = _rd.StorageBufferCreate((uint)vertexBytes.Length, vertexBytes);

            var heightBufferSize = (uint)vertexCount * sizeof(float);
            heightBuffer = _rd.StorageBufferCreate(heightBufferSize);

            var computeParams = new ComputeParams
            {
                numVertices = vertexCount,
                oceanDepthMultiplier = OceanDepthMultiplier,
                oceanFloorDepth = OceanFloorDepth,
                oceanFloorSmoothing = OceanFloorSmoothing,
                mountainBlend = MountainBlend
            };
            var paramsStructSize = Marshal.SizeOf<ComputeParams>();
            var paramsBytes = new byte[paramsStructSize];
            var handleParams = GCHandle.Alloc(computeParams, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(handleParams.AddrOfPinnedObject(), paramsBytes, 0, paramsBytes.Length);
            }
            finally
            {
                handleParams.Free();
            }

            paramsBuffer = _rd.StorageBufferCreate((uint)paramsBytes.Length, paramsBytes);

            var continentsParams = ContinentsNoiseSettings.GetNoiseParams(rng);
            var maskParams = MaskNoiseSettings.GetNoiseParams(rng);
            var mountainsParams = MountainsNoiseSettings.GetNoiseParams(rng);
            var allNoiseParams = continentsParams.Concat(maskParams).Concat(mountainsParams).ToArray();

            if (allNoiseParams.Length != 3 * 3 * 4)
            {
                throw new Exception(
                    $"Incorrect total number of noise parameters. Expected 36, Got {allNoiseParams.Length}");
            }

            var noiseParamsBytes = MemoryMarshal.AsBytes(allNoiseParams.AsSpan()).ToArray();
            noiseParamsBuffer = _rd.StorageBufferCreate((uint)noiseParamsBytes.Length, noiseParamsBytes);


            var uniforms = new Godot.Collections.Array<RDUniform>();
            var vertexUniform = new RDUniform { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 0 };
            vertexUniform.AddId(vertexBuffer);
            uniforms.Add(vertexUniform);
            var heightUniform = new RDUniform { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 1 };
            heightUniform.AddId(heightBuffer);
            uniforms.Add(heightUniform);
            var paramsUniform = new RDUniform
                { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 2 };
            paramsUniform.AddId(paramsBuffer);
            uniforms.Add(paramsUniform);
            var noiseParamsUniform = new RDUniform
                { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 3 };
            noiseParamsUniform.AddId(noiseParamsBuffer);
            uniforms.Add(noiseParamsUniform);

            uniformSet = _rd.UniformSetCreate(uniforms, _shaderRid, 0);
            if (!uniformSet.IsValid)
            {
                throw new Exception("Failed to create uniform set.");
            }

            const uint workGroupSizeX = 512;
            var numGroupsX = (uint)Mathf.Ceil((float)vertexCount / workGroupSizeX);

            var computeList = _rd.ComputeListBegin();
            _rd.ComputeListBindComputePipeline(computeList, _pipelineRid);
            _rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
            _rd.ComputeListDispatch(computeList, xGroups: numGroupsX, yGroups: 1, zGroups: 1);
            _rd.ComputeListEnd();

            _rd.Submit();
            _rd.Sync();

            var outputBytes = _rd.BufferGetData(heightBuffer);
            if (outputBytes != null && outputBytes.Length == heightBufferSize)
            {
                outputHeights = new float[vertexCount];
                MemoryMarshal.Cast<byte, float>(outputBytes).CopyTo(outputHeights);
            }
            else
            {
                throw new Exception(
                    $"Output buffer size mismatch or null. Expected {heightBufferSize}, Got {outputBytes?.Length ?? -1}");
            }
        }
        catch (Exception e)
        {
            GD.PushError(
                $"{nameof(PlanetHeight)} '{ResourceName}': Exception during compute shader execution: {e.Message}\n{e.StackTrace}");
            outputHeights = null;
        }
        finally
        {
            if (_rd != null)
            {
                if (uniformSet.IsValid) _rd.FreeRid(uniformSet);
                if (noiseParamsBuffer.IsValid) _rd.FreeRid(noiseParamsBuffer);
                if (paramsBuffer.IsValid) _rd.FreeRid(paramsBuffer);
                if (heightBuffer.IsValid) _rd.FreeRid(heightBuffer);
                if (vertexBuffer.IsValid) _rd.FreeRid(vertexBuffer);
            }
        }

        return outputHeights;
    }

    #endregion
}