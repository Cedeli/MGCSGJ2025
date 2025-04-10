using Godot;
using System;
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
            _isInitialized = false;
            EmitSignal(SignalName.ParametersChanged);
        }
    }

    // Unsure how to automate shader specific export variables...
    private float _testValue;

    [Export(PropertyHint.Range, "0.1, 50.0, 0.1")]
    public float TestValue
    {
        get => _testValue;
        set
        {
            if (Mathf.IsEqualApprox(_testValue, value)) return;
            _testValue = value;
            EmitSignal(SignalName.ParametersChanged);
        }
    }

    #endregion

    #region Compute State

    private RenderingDevice _rd;
    private Rid _shaderRid;
    private Rid _pipelineRid;
    private bool _isInitialized;

    // Also unsure how to automate structs here!
    [StructLayout(LayoutKind.Sequential)]
    private struct ComputeParamsUniform
    {
        public uint numVertices;
        public float testValue;
        public uint padding1;
        public uint padding2;
    }

    #endregion

    #region Godot Resource Lifecycle

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            CleanupComputeResources();
        }
    }

    #endregion

    #region Initialization and Cleanup

    // Lazy initialization
    private bool InitializeComputeResources()
    {
        if (_isInitialized) return true;
        if (string.IsNullOrEmpty(_shaderPath))
        {
            GD.PushError($"TerrainModifier: ShaderPath is not set.");
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
            GD.Print($"TerrainModifier '{ResourcePath}': Initialized successfully.");
            return true;
        }
        catch (Exception e)
        {
            GD.PushError($"TerrainModifier: Error initializing compute resources for '{_shaderPath}': {e.Message}");
            CleanupComputeResources();
            _isInitialized = false;
            return false;
        }
    }

    private void CleanupComputeResources()
    {
        GD.Print($"TerrainModifier '{ResourcePath}': Cleaning up compute resources.");
        if (_rd == null) return;

        if (_pipelineRid.IsValid) _rd.FreeRid(_pipelineRid);
        if (_shaderRid.IsValid) _rd.FreeRid(_shaderRid);

        _pipelineRid = default;
        _shaderRid = default;

        _rd.Free();
        _rd = null;

        _isInitialized = false;
    }

    #endregion

    #region Compute Shader Execution

    public float[] Execute(Vector3[] inputUnitVertices)
    {
        if (inputUnitVertices == null || inputUnitVertices.Length == 0)
        {
            GD.PushWarning("TerrainModifier: Execute called with null or empty vertices.");
            return null;
        }

        if (!_isInitialized && !InitializeComputeResources())
        {
            GD.PushError("TerrainModifier: Cannot Execute, resources not initialized.'");
            return null;
        }


        var vertexCount = inputUnitVertices.Length;
        Rid vertexBuffer = default;
        Rid heightBuffer = default;
        Rid paramsBuffer = default;
        Rid uniformSet = default;
        float[] outputHeights;

        try
        {
            // Input Vertices (SSBO - std430 alignment)
            var vec3PaddedSize = sizeof(float) * 4;
            var vertexBytes = new byte[vertexCount * vec3PaddedSize];
            for (var i = 0; i < vertexCount; ++i)
            {
                var vert = inputUnitVertices[i];
                var byteOffset = i * vec3PaddedSize;
                Buffer.BlockCopy(BitConverter.GetBytes(vert.X), 0, vertexBytes, byteOffset + 0, sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(vert.Y), 0, vertexBytes, byteOffset + sizeof(float),
                    sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(vert.Z), 0, vertexBytes, byteOffset + sizeof(float) * 2,
                    sizeof(float));
            }

            vertexBuffer = _rd.StorageBufferCreate((uint)vertexBytes.Length, vertexBytes);

            // Output Heights (SSBO - float size)
            var heightBufferSize = (uint)vertexCount * sizeof(float);
            heightBuffer = _rd.StorageBufferCreate(heightBufferSize);

            // Parameters (UBO - std140 alignment)
            var computeParams = new ComputeParamsUniform
            {
                numVertices = (uint)vertexCount,
                testValue = this.TestValue,
                padding1 = 0,
                padding2 = 0
            };
            var paramsStructSize = Marshal.SizeOf<ComputeParamsUniform>();
            var paramsBytes = new byte[paramsStructSize];

            var handle = GCHandle.Alloc(computeParams, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(handle.AddrOfPinnedObject(), paramsBytes, 0, paramsBytes.Length);
            }
            finally
            {
                handle.Free();
            }

            paramsBuffer = _rd.UniformBufferCreate((uint)paramsBytes.Length, paramsBytes);

            var uniforms = new Godot.Collections.Array<RDUniform>();

            var vertexUniform = new RDUniform { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 0 };
            vertexUniform.AddId(vertexBuffer);
            uniforms.Add(vertexUniform);
            var heightUniform = new RDUniform { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 1 };
            heightUniform.AddId(heightBuffer);
            uniforms.Add(heightUniform);
            var paramsUniform = new RDUniform { UniformType = RenderingDevice.UniformType.UniformBuffer, Binding = 2 };
            paramsUniform.AddId(paramsBuffer);
            uniforms.Add(paramsUniform);
            uniformSet = _rd.UniformSetCreate(uniforms, _shaderRid, 0);

            // TODO: Make this configurable or readable from shader?
            uint workGroupSizeX = 512;
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
                Buffer.BlockCopy(outputBytes, 0, outputHeights, 0, outputBytes.Length);
            }
            else
            {
                throw new Exception(
                    $"Output buffer size mismatch. Expected {heightBufferSize}, Got {outputBytes?.Length ?? -1}");
            }
        }
        catch (Exception e)
        {
            GD.PushError(
                $"TerrainModifier: Exception during compute shader execution: {e.Message} \n {e.StackTrace}");
            outputHeights = null;
        }
        finally
        {
            if (uniformSet.IsValid) _rd?.FreeRid(uniformSet);
            if (paramsBuffer.IsValid) _rd?.FreeRid(paramsBuffer);
            if (heightBuffer.IsValid) _rd?.FreeRid(heightBuffer);
            if (vertexBuffer.IsValid) _rd?.FreeRid(vertexBuffer);
        }

        return outputHeights;
    }

    #endregion
}