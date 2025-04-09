using Godot;
using System;

public interface IInputReceiver
{
    void OnMoveInput(Vector2 direction);
    void OnLookInput(Vector2 lookDelta);
    void SetInputManager(InputManager manager);
    void SetInputBuffer(InputBuffer buffer);
}
