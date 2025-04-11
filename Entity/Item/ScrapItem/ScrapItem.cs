using System;
using Godot;

public partial class ScrapItem : Item
{
    [Export(PropertyHint.Range, "10.0, 100.0, 5.0")]
    public float HullToRestore = 50.0f;

    private Ship _shipCache = null;

    protected override bool ApplyEffect(Player player)
    {
        if (player == null)
            return false;
        if (_shipCache == null || !IsInstanceValid(_shipCache))
        {
            _shipCache = GetNodeFromGroupHelper<Ship>(Ship.ShipGroup);
        }
        GD.Print($"Applying {HullToRestore} hull repair to {_shipCache.Name}");
        return _shipCache.HealHull(HullToRestore);
    }

    private T GetNodeFromGroupHelper<T>(string group)
        where T : Node
    {
        var nodes = GetTree().GetNodesInGroup(group);
        if (nodes.Count > 0 && nodes[0] is T typedNode)
            return typedNode;
        return null;
    }
}
