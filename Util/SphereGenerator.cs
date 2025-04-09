using Godot;
using System;
using System.Collections.Generic;

// Ported from: https://github.com/SebLague/Solar-System/blob/Episode_02/Assets/Celestial%20Body/Scripts/SphereMesh.cs
[Tool]
public partial class SphereGenerator : Node3D
{
    private MeshInstance3D _meshInstance;
    private Node3D _previewPointsContainer;
    private ArrayMesh _mesh;

    private const string PreviewContainerName = "PreviewPoints";
    private const string MeshInstanceName = "GeneratedMesh";
    
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
            foreach (var item in collection)
            {
                Add(item);
            }
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

    private int _resolution = 1;
    [Export(PropertyHint.Range, "0,200,1")]
    public int Resolution
    {
        get => _resolution;
        set
        {
            if (_resolution == value) return;
            _resolution = Mathf.Max(0, value);
            if (Engine.IsEditorHint() || IsInsideTree())
            {
                CallDeferred(nameof(GenerateMesh));
            }
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
            if (Engine.IsEditorHint() || IsInsideTree())
            {
                CallDeferred(nameof(GenerateMesh));
            }
        }
    }
    
    private static readonly Vector3[] BaseVertices = {
        Vector3.Up,
        Vector3.Left,
        Vector3.Back,
        Vector3.Right,
        Vector3.Forward,
        Vector3.Down
    };
    
    private static readonly int[] VertexPairs = {
        0, 1, 0, 2, 0, 3, 0, 4, 1, 2, 2, 3, 3, 4, 4, 1, 5, 1, 5, 2, 5, 3, 5, 4
    };
    
    private static readonly int[] EdgeTriplets = {
        0, 1, 4, 1, 2, 5, 2, 3, 6, 3, 0, 7, 8, 9, 4, 9, 10, 5, 10, 11, 6, 11, 8, 7
    };

    public override void _Ready()
    {
        EnsureRequiredNodes();
        GenerateMesh();
    }

    private void EnsureRequiredNodes()
    {
        _meshInstance = GetNodeOrNull<MeshInstance3D>(MeshInstanceName);
        if (_meshInstance == null)
        {
            _meshInstance = new MeshInstance3D();
            _meshInstance.Name = MeshInstanceName;
            AddChild(_meshInstance);
            if (Engine.IsEditorHint() && GetTree() != null && GetTree().EditedSceneRoot == GetOwner())
            {
                _meshInstance.Owner = GetTree().EditedSceneRoot;
            }
            else if (Engine.IsEditorHint() && Owner != null)
            {
                _meshInstance.Owner = Owner;
            }
        }

        _previewPointsContainer = GetNodeOrNull<Node3D>(PreviewContainerName);
        if (_previewPointsContainer != null) return;
        _previewPointsContainer = new Node3D();
        _previewPointsContainer.Name = PreviewContainerName;
        AddChild(_previewPointsContainer);
        if (Engine.IsEditorHint() && GetTree() != null && GetTree().EditedSceneRoot == GetOwner())
        {
            _previewPointsContainer.Owner = GetTree().EditedSceneRoot;
        }
        else if (Engine.IsEditorHint() && Owner != null)
        {
            _previewPointsContainer.Owner = Owner;
        }
    }

    public void GenerateMesh()
    {
        if (_meshInstance == null) EnsureRequiredNodes();
        if (_meshInstance == null) return;
        
        var numDivisions = Mathf.Max(0, _resolution);
        var numVertsPerFace = ((numDivisions + 3) * (numDivisions + 3) - (numDivisions + 3)) / 2;
        var numVerts = numVertsPerFace * 8 - (numDivisions + 2) * 12 + 6;
        var numTrisPerFace = (numDivisions + 1) * (numDivisions + 1);
        var totalIndices = numTrisPerFace * 8 * 3;
        
        var vertices = new FixedSizeList<Vector3>(numVerts);
        var triangles = new FixedSizeList<int>(totalIndices);
        
        vertices.AddRange(BaseVertices);
        
        var edges = new Edge[12];
        for (var i = 0; i < VertexPairs.Length; i += 2)
        {
            var startVertex = vertices.Items[VertexPairs[i]];
            var endVertex = vertices.Items[VertexPairs[i + 1]];

            var edgeVertexIndices = new int[numDivisions + 2];
            edgeVertexIndices[0] = VertexPairs[i];
            
            for (var divisionIndex = 0; divisionIndex < numDivisions; divisionIndex++)
            {
                var t = (divisionIndex + 1f) / (numDivisions + 1f);
                edgeVertexIndices[divisionIndex + 1] = vertices.Count;
                vertices.Add(Slerp(startVertex, endVertex, t));
            }

            edgeVertexIndices[numDivisions + 1] = VertexPairs[i + 1];
            var edgeIndex = i / 2;
            edges[edgeIndex] = new Edge(edgeVertexIndices);
        }
        
        for (var i = 0; i < EdgeTriplets.Length; i += 3)
        {
            var faceIndex = i / 3;
            var reverse = faceIndex >= 4;
            CreateFace(edges[EdgeTriplets[i]], edges[EdgeTriplets[i + 1]], edges[EdgeTriplets[i + 2]], 
                      reverse, vertices, triangles, numVertsPerFace, numDivisions);
        }
        
        CreateMesh(vertices.Items, triangles.Items, vertices.Count, triangles.Count);
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
            return ((normFrom * (1.0f - t)) + (normTo * t)).Normalized();
        }
        
        var sinTheta = Mathf.Sin(theta);
        var wa = Mathf.Sin((1.0f - t) * theta) / sinTheta;
        var wb = Mathf.Sin(t * theta) / sinTheta;
        
        return (normFrom * wa + normTo * wb).Normalized();
    }

    private void CreateFace(Edge sideA, Edge sideB, Edge bottom, bool reverse, 
                           FixedSizeList<Vector3> vertices, FixedSizeList<int> triangles,
                           int numVertsPerFace, int numDivisions)
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
                vertexMap.Add(vertices.Count);
                vertices.Add(Slerp(sideAVertex, sideBVertex, t));
            }
            
            vertexMap.Add(sideB.VertexIndices[i]);
        }
        
        for (var i = 0; i < numPointsInEdge; i++)
        {
            vertexMap.Add(bottom.VertexIndices[i]);
        }
        
        var numRows = numDivisions + 1;
        for (var row = 0; row < numRows; row++)
        {
            var topVertex = ((row + 1) * (row + 1) - row - 1) / 2;
            var bottomVertex = ((row + 2) * (row + 2) - row - 2) / 2;

            var numTrianglesInRow = 1 + 2 * row;
            for (var column = 0; column < numTrianglesInRow; column++)
            {
                int v0, v1, v2;

                if (column % 2 == 0)
                {
                    v0 = topVertex;
                    v1 = bottomVertex + 1;
                    v2 = bottomVertex;
                    topVertex++;
                    bottomVertex++;
                }
                else
                {
                    v0 = topVertex;
                    v1 = bottomVertex;
                    v2 = topVertex - 1;
                }

                triangles.Add(vertexMap.Items[v0]);
                triangles.Add(vertexMap.Items[(reverse) ? v2 : v1]);
                triangles.Add(vertexMap.Items[(reverse) ? v1 : v2]);
            }
        }
    }

    private void CreateMesh(Vector3[] vertices, int[] triangles, int vertexCount, int indexCount)
    {
        for (var i = 0; i < vertexCount; i++)
        {
            vertices[i] *= _radius;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        
        var finalVertices = new Vector3[vertexCount];
        var finalIndices = new int[indexCount];
        var normals = new Vector3[vertexCount];
        
        Array.Copy(vertices, finalVertices, vertexCount);
        Array.Copy(triangles, finalIndices, indexCount);
        
        for (var i = 0; i < vertexCount; i++)
        {
            normals[i] = vertices[i].Normalized();
        }
        
        arrays[(int)Mesh.ArrayType.Vertex] = finalVertices;
        arrays[(int)Mesh.ArrayType.Index] = finalIndices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        
        if (_mesh == null)
        {
            _mesh = new ArrayMesh();
        }
        else
        {
            _mesh.ClearSurfaces();
        }

        _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _meshInstance.Mesh = _mesh;
    }
}