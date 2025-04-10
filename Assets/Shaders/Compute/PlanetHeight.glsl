#[compute]
#version 450

layout(local_size_x = 512, local_size_y = 1, local_size_z = 1) in;

// SSBO using std430 layout (vec3 requires 16-byte alignment)
layout(set = 0, binding = 0, std430) readonly buffer VertexBuffer {
    vec3 vertices[];
};

// SSBO using std430 layout (float is 4 bytes)
layout(set = 0, binding = 1, std430) writeonly buffer HeightBuffer {
    float heights[];
};

// UBO using std140 layout
layout(set = 0, binding = 2, std140) uniform ComputeParams {
    uint numVertices;
    float testValue;
} params;

void main() {
    uint id = gl_GlobalInvocationID.x;

    uint numVerts = params.numVertices;
    float tValue = params.testValue;

    if (id >= numVerts) {
        return;
    }

    vec3 vertexPos = vertices[id];

    heights[id] = 1.0 + sin(vertexPos.y * tValue) * 0.05;
}