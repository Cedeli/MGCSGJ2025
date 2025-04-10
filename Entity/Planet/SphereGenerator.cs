using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class SphereGenerator : Node
{
    #region Signals

    [Signal]
    public delegate void ParametersChangedEventHandler();

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
                ConnectToModifierSignal();
            }

            EmitSignal(SignalName.ParametersChanged);
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
            EmitSignal(SignalName.ParametersChanged);
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
            EmitSignal(SignalName.ParametersChanged);
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

        public Edge(int[] vi)
        {
            VertexIndices = vi;
        }
    }

    #endregion

    #region Static Sphere Data

    private static readonly Vector3[] BaseVertices =
        { Vector3.Up, Vector3.Left, Vector3.Back, Vector3.Right, Vector3.Forward, Vector3.Down };

    private static readonly int[] VertexPairs =
        { 0, 1, 0, 2, 0, 3, 0, 4, 1, 2, 2, 3, 3, 4, 4, 1, 5, 1, 5, 2, 5, 3, 5, 4 };

    private static readonly int[] EdgeTriplets =
        { 0, 1, 4, 1, 2, 5, 2, 3, 6, 3, 0, 7, 8, 9, 4, 9, 10, 5, 10, 11, 6, 11, 8, 7 };

    #endregion

    #region Godot Lifecycle Methods

    public override void _Ready()
    {
        ConnectToModifierSignal();
    }

    public override void _ExitTree()
    {
        if (_terrainModifier != null && _terrainModifier.IsConnected(PlanetHeight.SignalName.ParametersChanged,
                Callable.From(OnModifierParametersChanged)))
        {
            _terrainModifier.Disconnect(PlanetHeight.SignalName.ParametersChanged,
                Callable.From(OnModifierParametersChanged));
        }
    }

    #endregion


    #region Mesh Generation

    public ArrayMesh GenerateMeshData()
    {
        GD.Print($"SphereGenerator: Generating mesh data (Res: {Resolution}, Radius: {Radius})");

        if (_terrainModifier == null)
        {
            GD.PushWarning(
                $"{nameof(SphereGenerator)}: {nameof(TerrainModifier)} is not set. Generating basic sphere.");
        }

        var numDivisions = Mathf.Max(0, _resolution);
        var numVertsPerFace = ((numDivisions + 3) * (numDivisions + 3) - (numDivisions + 3)) / 2;
        var numVerts = numVertsPerFace * 8 - (numDivisions + 2) * 12 + 6;
        var numTrisPerFace = (numDivisions + 1) * (numDivisions + 1);
        var totalIndices = numTrisPerFace * 8 * 3;
        
        if (numVerts <= 0 || totalIndices <= 0)
        {
            GD.PushError("SphereGenerator: Calculated zero vertices or indices. Aborting mesh generation.");
            return null;
        }

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
            heights = _terrainModifier.Execute(unitVertices.Items);
            if (heights == null)
            {
                GD.PushError(
                    $"{nameof(SphereGenerator)}: TerrainModifier execution failed. Falling back to basic sphere.");
            }
            else if (heights.Length != unitVertices.Count)
            {
                GD.PushError(
                    $"{nameof(SphereGenerator)}: TerrainModifier returned incorrect number of heights ({heights.Length} vs {unitVertices.Count}). Falling back.");
                heights = null;
            }
        }

        var finalVertices = new Vector3[unitVertices.Count];

        return ApplyHeightsAndCreateMesh(unitVertices.Items, triangles.Items, unitVertices.Count, triangles.Count,
            heights, finalVertices);
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

    private ArrayMesh ApplyHeightsAndCreateMesh(Vector3[] unitVertices, int[] triangles, int vertexCount,
        int indexCount,
        IReadOnlyList<float> heights, Vector3[] finalVerticesOutput)
    {
        for (var i = 0; i < vertexCount; i++)
        {
            var unitNormal = unitVertices[i].Normalized();
            var heightFactor = (heights != null && i < heights.Count) ? heights[i] : 1.0f;
            finalVerticesOutput[i] = unitNormal * heightFactor * _radius;
        }
        
        var calculatedNormals = RecalculateNormals(finalVerticesOutput, triangles, vertexCount, indexCount);

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        var finalIndices = new int[indexCount];
        Array.Copy(triangles, finalIndices, indexCount);

        arrays[(int)Mesh.ArrayType.Vertex] = finalVerticesOutput;
        arrays[(int)Mesh.ArrayType.Index] = finalIndices;
        arrays[(int)Mesh.ArrayType.Normal] = calculatedNormals;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        return mesh;
    }
    
    private static Vector3[] RecalculateNormals(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles, int vertexCount, int indexCount)
    {
        var calculatedNormals = new Vector3[vertexCount];
        
        for (var i = 0; i < indexCount; i += 3)
        {
            var i0 = triangles[i];
            var i1 = triangles[i + 1];
            var i2 = triangles[i + 2];
            
            var v0 = vertices[i0];
            var v1 = vertices[i1];
            var v2 = vertices[i2];
            
            var edge1 = v1 - v0;
            var edge2 = v2 - v0;

            var faceNormal = edge1.Cross(edge2) * -1.0f;

            calculatedNormals[i0] += faceNormal;
            calculatedNormals[i1] += faceNormal;
            calculatedNormals[i2] += faceNormal;
        }
        
        for (var i = 0; i < vertexCount; i++)
        {
            calculatedNormals[i] = calculatedNormals[i].Normalized();
        }

        return calculatedNormals;
    }

    #endregion

    #region Signal Handlers

    private void ConnectToModifierSignal()
    {
        if (_terrainModifier == null || IsConnected(PlanetHeight.SignalName.ParametersChanged,
                Callable.From(OnModifierParametersChanged)))
        {
            return;
        }

        var err = _terrainModifier.Connect(PlanetHeight.SignalName.ParametersChanged,
            Callable.From(OnModifierParametersChanged));
        if (err != Error.Ok)
        {
            GD.PushError($"SphereGenerator: Failed to connect to TerrainModifier signal: {err}");
        }
    }

    private void OnModifierParametersChanged()
    {
        EmitSignal(SignalName.ParametersChanged);
    }

    #endregion
}