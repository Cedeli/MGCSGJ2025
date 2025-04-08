using Godot;
using System;

public partial class GravityShape : CollisionShape3D
{
    public void SetParameters(float x, float y, float z)
    {
        SetScale(new Vector3(x, y, z));
        
    }
}
