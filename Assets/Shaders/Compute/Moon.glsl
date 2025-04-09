#[compute]
#version 450

layout(local_size_x = 512, local_size_y = 1, local_size_z = 1) in;

// Storage buffer (vec3 often aligned to 16 bytes in std430)
layout(set = 0, binding = 0, std430) readonly buffer VertexBuffer {
    vec3 vertices[];
};

// Storage buffer
layout(set = 0, binding = 1, std430) writeonly buffer HeightBuffer {
    float heights[];
};

// Uniform Buffer Object - Use std140 for standard alignment
// Binding = 2, Set = 0
layout(set = 0, binding = 2, std140) uniform ComputeParams {
    uint numVertices; // Base align 4, offset 0
    float testValue;  // Base align 4, offset 4
    // The shader automatically expects padding here to meet std140 rules
    // The total block size will likely be aligned to 16 bytes.
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