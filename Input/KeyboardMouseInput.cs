using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class KeyboardMouseInput : InputProvider
{
    [Export]
    public float MouseSensitivity = 0.25f;

    [Export]
    public bool InvertY;

    private readonly HashSet<string> _bufferableActions = ["jump"];

    public override void _Process(double delta)
    {
        ProcessMovementInput();
        ProcessBufferedActionInputs();

        if (Input.IsActionPressed("shoot"))
        {
            EmitShootInput();
        }

        if (Input.IsActionJustPressed("reload"))
        {
            EmitReloadInput();
        }

        if (Input.IsActionJustPressed("pause"))
        {
            EmitPauseInput();
        }
    }

    private void ProcessMovementInput()
    {
        var moveDirection = Input.GetVector(
            "move_left",
            "move_right",
            "move_forward",
            "move_backward"
        );
        EmitMoveInput(moveDirection);
    }

    private void ProcessBufferedActionInputs()
    {
        foreach (var action in _bufferableActions)
        {
            if (Input.IsActionJustPressed(action))
            {
                BufferAction(action);
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        ProcessMouseLook(@event);
    }

    private void ProcessMouseLook(InputEvent @event)
    {
        if (
            @event is not InputEventMouseMotion mouseMotion
            || Input.MouseMode != Input.MouseModeEnum.Captured
        )
            return;
        var lookDelta = new Vector2(
            mouseMotion.Relative.X,
            mouseMotion.Relative.Y * (InvertY ? -1f : 1f)
        );
        EmitLookInput(lookDelta);
    }
}
