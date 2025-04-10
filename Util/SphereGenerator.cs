using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class SphereGenerator : Node3D
{
    #region Members

    private MeshInstance3D _meshInstance;
    private ArrayMesh _mesh;
    
    private const string MeshInstanceName = "GeneratedMesh";

    #endregion

    #region Exports
    
    private PlanetHeight _terrainModifier;

    [Export]
    public PlanetHeight TerrainModifier
    {
        get => _terrainModifier;
        set
        {
            if (_terrainModifier == value) return;
            
            if (_terrainModifier != null && _terrainModifier.IsConnected(PlanetHeight.SignalName.ParametersChanged,
                    Callable.From(OnModifierParametersChanged)))
            {
                _terrainModifier.Disconnect(PlanetHeight.SignalName.ParametersChanged,
                    Callable.From(OnModifierParametersChanged));
            }

            _terrainModifier = value;
            
            if (_terrainModifier != null)
            {
                if (!_terrainModifier.IsConnected(PlanetHeight.SignalName.ParametersChanged,
                        Callable.From(OnModifierParametersChanged)))
                {
                    var err = _terrainModifier.Connect(PlanetHeight.SignalName.ParametersChanged,
                        Callable.From(OnModifierParametersChanged));
                    if (err != Error.Ok)
                        GD.PushError($"SphereGenerator: Failed to connect to TerrainModifier signal: {err}");
                }
            }

            if (IsNodeReady())
            {
                CallDeferred(nameof(GenerateMesh));
            }
        }
    }

    
    private int _resolution = 10;

    [Export(PropertyHint.Range, "0,200,1")]
    public int Resolution
    {
        get => _resolution;
        set
        {
            if (_resolution == value) return;
            _resolution = Mathf.Max(0, value);
            if (IsNodeReady()) CallDeferred(nameof(GenerateMesh));
        }
    }

    private float _radius = 1.0f;

    [Export(PropertyHint.Range, "0.1,100.0,0.1")]
    public float Radius
    {
        get => _radius;
        set
        {
            if (Mathf.IsEqualApprox(_radius, value)) return;
            _radius = Mathf.Max(0.1f, value);
            if (IsNodeReady()) CallDeferred(nameof(GenerateMesh));
        }
    }

    #endregion

    #region Data Structures

    private class FixedSizeList<T>
    {
        public T[] Items;
        public int Count;

        public FixedSizeList(int capacity)
        {
            Items = new T[capacity];
        }

        public int Add(T item)
        {
            var index = Count;
            Items[Count++] = item;
            return index;
        }

        public void AddRange(IEnumerable<T> collection)
        {
            foreach (var item in collection) Add(item);
        }
    }

    private class Edge
    {
        public int[] VertexIndices;

        public Edge(int[] vertexIndices)
        {
            VertexIndices = vertexIndices;
        }
    }

    #endregion

    #region Static Sphere Data

    private static readonly Vector3[] BaseVertices =
    [
        Vector3.Up, Vector3.Left, Vector3.Back, Vector3.Right, Vector3.Forward, Vector3.Down
    ];

    private static readonly int[] VertexPairs =
    [
        0, 1, 0, 2, 0, 3, 0, 4, 1, 2, 2, 3, 3, 4, 4, 1, 5, 1, 5, 2, 5, 3, 5, 4
    ];

    private static readonly int[] EdgeTriplets =
    [
        0, 1, 4, 1, 2, 5, 2, 3, 6, 3, 0, 7, 8, 9, 4, 9, 10, 5, 10, 11, 6, 11, 8, 7
    ];

    #endregion

    #region Godot Lifecycle Methods

    public override void _Ready()
    {
        EnsureRequiredNodes();
        if (_terrainModifier != null && !_terrainModifier.IsConnected(PlanetHeight.SignalName.ParametersChanged,
                Callable.From(OnModifierParametersChanged)))
        {
            var err = _terrainModifier.Connect(PlanetHeight.SignalName.ParametersChanged,
                Callable.From(OnModifierParametersChanged));
            if (err != Error.Ok)
                GD.PushError($"SphereGenerator: Failed to connect to TerrainModifier signal in _Ready: {err}");
        }

        GenerateMesh();
    }
    
    #endregion
    
    #region Node Management

    private void EnsureRequiredNodes()
    {
        _meshInstance = GetNodeOrNull<MeshInstance3D>(MeshInstanceName);
        if (_meshInstance != null) return;
        _meshInstance = new MeshInstance3D { Name = MeshInstanceName };
        AddChild(_meshInstance);
        if (Engine.IsEditorHint())
        {
            _meshInstance.Owner = GetTree()?.EditedSceneRoot ?? Owner;
        }
    }

    #endregion

    #region Mesh Generation

    public void GenerateMesh()
    {
        if (!IsNodeReady())
        {
            CallDeferred(nameof(GenerateMesh));
            return;
        }

        EnsureRequiredNodes();
        if (_meshInstance == null) return;
        
        var numDivisions = Mathf.Max(0, _resolution);
        var numVertsPerFace = ((numDivisions + 3) * (numDivisions + 3) - (numDivisions + 3)) / 2;
        var numVerts = numVertsPerFace * 8 - (numDivisions + 2) * 12 + 6;
        var numTrisPerFace = (numDivisions + 1) * (numDivisions + 1);
        var totalIndices = numTrisPerFace * 8 * 3;
        var unitVertices = new FixedSizeList<Vector3>(numVerts);
        var triangles = new FixedSizeList<int>(totalIndices);
        unitVertices.AddRange(BaseVertices);
        var edges = new Edge[12];
        for (var i = 0; i < VertexPairs.Length; i += 2)
        {
            var startVertex = unitVertices.Items[VertexPairs[i]];
            var endVertex = unitVertices.Items[VertexPairs[i + 1]];
            var edgeVertexIndices = new int[numDivisions + 2];
            edgeVertexIndices[0] = VertexPairs[i];
            for (var divisionIndex = 0; divisionIndex < numDivisions; divisionIndex++)
            {
                var t = (divisionIndex + 1f) / (numDivisions + 1f);
                edgeVertexIndices[divisionIndex + 1] = unitVertices.Add(Slerp(startVertex, endVertex, t));
            }

            edgeVertexIndices[numDivisions + 1] = VertexPairs[i + 1];
            edges[i / 2] = new Edge(edgeVertexIndices);
        }

        for (var i = 0; i < EdgeTriplets.Length; i += 3)
        {
            var reverse = (i / 3) >= 4;
            CreateFace(edges[EdgeTriplets[i]], edges[EdgeTriplets[i + 1]], edges[EdgeTriplets[i + 2]], reverse,
                unitVertices, triangles, numVertsPerFace);
        }

        
        float[] heights = null;
        if (_terrainModifier != null)
        {
            GD.Print($"SphereGenerator: Executing TerrainModifier...");
            heights = _terrainModifier.Execute(unitVertices.Items);
        }
        
        var finalVertices = new Vector3[unitVertices.Count];
        ApplyHeightsAndCreateMesh(unitVertices.Items, triangles.Items, unitVertices.Count, triangles.Count, heights,
            finalVertices);
    }
    
    private Vector3 Slerp(Vector3 from, Vector3 to, float t)
    {
        var normFrom = from.Normalized();
        var normTo = to.Normalized();
        var dot = normFrom.Dot(normTo);
        dot = Mathf.Clamp(dot, -1.0f, 1.0f);
        var theta = Mathf.Acos(dot);
        if (Mathf.IsZeroApprox(theta))
        {
            return (normFrom + (normTo - normFrom) * t).Normalized();
        }

        var sinTheta = Mathf.Sin(theta);
        var wa = Mathf.Sin((1.0f - t) * theta) / sinTheta;
        var wb = Mathf.Sin(t * theta) / sinTheta;
        return (normFrom * wa + normTo * wb).Normalized();
    }

    private void CreateFace(Edge sideA, Edge sideB, Edge bottom, bool reverse, FixedSizeList<Vector3> vertices,
        FixedSizeList<int> triangles, int numVertsPerFace)
    {
        var numPointsInEdge = sideA.VertexIndices.Length;
        var vertexMap = new FixedSizeList<int>(numVertsPerFace);
        vertexMap.Add(sideA.VertexIndices[0]);
        for (var i = 1; i < numPointsInEdge - 1; i++)
        {
            vertexMap.Add(sideA.VertexIndices[i]);
            var sideAVertex = vertices.Items[sideA.VertexIndices[i]];
            var sideBVertex = vertices.Items[sideB.VertexIndices[i]];
            var numInnerPoints = i - 1;
            for (var j = 0; j < numInnerPoints; j++)
            {
                var t = (j + 1f) / (numInnerPoints + 1f);
                vertexMap.Add(vertices.Add(Slerp(sideAVertex, sideBVertex, t)));
            }

            vertexMap.Add(sideB.VertexIndices[i]);
        }

        for (var i = 0; i < numPointsInEdge; i++)
        {
            vertexMap.Add(bottom.VertexIndices[i]);
        }

        var numRows = numPointsInEdge - 1;
        for (var row = 0; row < numRows; row++)
        {
            var topVertexIndex = row * (row + 1) / 2;
            var bottomVertexIndex = (row + 1) * (row + 2) / 2;
            var numTrianglesInRow = 1 + 2 * row;
            for (var column = 0; column < numTrianglesInRow; column++)
            {
                int v0, v1, v2;
                if (column % 2 == 0)
                {
                    v0 = topVertexIndex;
                    v1 = bottomVertexIndex + 1;
                    v2 = bottomVertexIndex;
                    topVertexIndex++;
                    bottomVertexIndex++;
                }
                else
                {
                    v0 = topVertexIndex;
                    v1 = bottomVertexIndex;
                    v2 = topVertexIndex - 1;
                }

                triangles.Add(vertexMap.Items[v0]);
                triangles.Add(vertexMap.Items[reverse ? v2 : v1]);
                triangles.Add(vertexMap.Items[reverse ? v1 : v2]);
            }
        }
    }
    
    private void ApplyHeightsAndCreateMesh(Vector3[] unitVertices, int[] triangles, int vertexCount, int indexCount,
        IReadOnlyList<float> heights, Vector3[] finalVerticesOutput)
    {
        var normals = new Vector3[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            normals[i] = unitVertices[i].Normalized();
            var heightFactor = (heights != null) ? heights[i] : 1.0f;
            finalVerticesOutput[i] = normals[i] * heightFactor * _radius;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        var finalIndices = new int[indexCount];
        Array.Copy(triangles, finalIndices, indexCount);
        arrays[(int)Mesh.ArrayType.Vertex] = finalVerticesOutput;
        arrays[(int)Mesh.ArrayType.Index] = finalIndices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        if (_mesh == null) _mesh = new ArrayMesh();
        else _mesh.ClearSurfaces();
        _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        if (_meshInstance != null) _meshInstance.Mesh = _mesh;
        else GD.PushError("SphereGenerator: MeshInstance is null when trying to assign mesh.");
    }

    #endregion

    #region Signal Handlers
    
    private void OnModifierParametersChanged()
    {
        GD.Print("SphereGenerator: Modifier parameters changed, regenerating mesh.");
        if (IsNodeReady())
        {
            CallDeferred(nameof(GenerateMesh));
        }
    }

    #endregion
}