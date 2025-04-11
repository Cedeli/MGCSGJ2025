using System;
using Godot;

public abstract partial class InputProvider : Node
{
    protected InputBuffer Buffer;

    [Signal]
    public delegate void MoveInputEventHandler(Vector2 direction);

    [Signal]
    public delegate void LookInputEventHandler(Vector2 lookDelta);

    [Signal]
    public delegate void JumpInputEventHandler();

    [Signal]
    public delegate void ShootInputEventHandler();

    [Signal]
    public delegate void ReloadInputEventHandler();

    [Signal]
    public delegate void PauseInputEventHandler();

    protected void EmitMoveInput(Vector2 direction) => EmitSignal(SignalName.MoveInput, direction);

    protected void EmitLookInput(Vector2 lookDelta) => EmitSignal(SignalName.LookInput, lookDelta);

    protected void EmitJumpInput() => EmitSignal(SignalName.JumpInput);

    protected void EmitShootInput() => EmitSignal(SignalName.ShootInput);

    protected void EmitReloadInput() => EmitSignal(SignalName.ReloadInput);

    protected void EmitPauseInput() => EmitSignal(SignalName.PauseInput);

    public void SetInputBuffer(InputBuffer buffer)
    {
        Buffer = buffer;
    }

    protected void BufferAction(string actionName, Vector2? direction = null)
    {
        Buffer?.BufferAction(actionName, direction);
    }
}
