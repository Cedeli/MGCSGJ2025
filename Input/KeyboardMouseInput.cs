using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class KeyboardMouseInput : InputProvider
{
    [Export] public float MouseSensitivity = 0.25f;
    [Export] public bool InvertY;
    
    // Only jump for now, unsure what else to buffer...
    private readonly HashSet<string> _bufferableActions = ["jump"];
    
    public override void _Process(double delta)
    {
        ProcessMovementInput();
        ProcessActionInputs();
    }
    
    private void ProcessMovementInput()
    {
        var moveDirection = Input.GetVector("move_left", "move_right", 
            "move_forward", "move_backward");
        EmitMoveInput(moveDirection);
    }
    
    private void ProcessActionInputs()
    {
        foreach (var action in _bufferableActions.Where(action => Input.IsActionJustPressed(action)))
        {
            BufferAction(action);
        }
    }
    
    public override void _Input(InputEvent @event)
    {
        ProcessMouseLook(@event);
    }
    
    private void ProcessMouseLook(InputEvent @event)
    {
        if (@event is not InputEventMouseMotion mouseMotion || Input.MouseMode != Input.MouseModeEnum.Captured) return;
        var lookDelta = new Vector2(
            mouseMotion.Relative.X,
            mouseMotion.Relative.Y * (InvertY ? -1f : 1f)
        );
        EmitLookInput(lookDelta);
    }
}