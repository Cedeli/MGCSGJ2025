using Godot;
using System;

public partial class InputManager : Node
{
	// If ever we decide to add gamepad support...
	public enum InputType { KeyboardMouse, Gamepad }
	public enum MovementMode { Grounded, Ship }
	
	[Signal] public delegate void InputTypeChangedEventHandler(InputType newType);
	[Signal] public delegate void MovementModeChangedEventHandler(MovementMode newMode);
	
	[Export] private InputBuffer _inputBuffer;
	[Export] private KeyboardMouseInput _keyboardMouseInput;
	
	private InputType _currentInputType = InputType.KeyboardMouse;
	private MovementMode _currentMovementMode = MovementMode.Grounded;
	
	public MovementMode CurrentMovementMode => _currentMovementMode;
	
	public override void _Ready()
	{
		ConnectInputReceivers();
		_keyboardMouseInput?.SetInputBuffer(_inputBuffer);
	}
	
	private void ConnectInputReceivers()
	{
		var receivers = GetTree().GetNodesInGroup("input_receivers");
		
		foreach (var node in receivers)
		{
			if (node is not IInputReceiver receiver) continue;
			
			receiver.SetInputBuffer(_inputBuffer);

			if (_keyboardMouseInput == null) continue;
			_keyboardMouseInput.MoveInput += receiver.OnMoveInput;
			_keyboardMouseInput.LookInput += receiver.OnLookInput;
		}
	}
	
	public void ChangeMovementMode(MovementMode newMode)
	{
		if (newMode == _currentMovementMode) return;
		
		_currentMovementMode = newMode;
		EmitSignal(SignalName.MovementModeChanged, (int)_currentMovementMode);
	}
	
	public override void _UnhandledInput(InputEvent @event)
	{
		var newType = DetectInputType(@event);
		if (newType == _currentInputType) return;
		
		_currentInputType = newType;
		EmitSignal(SignalName.InputTypeChanged, (int)_currentInputType);
	}
	
	private InputType DetectInputType(InputEvent @event)
	{
		return @event switch
		{
			InputEventKey or InputEventMouseMotion or InputEventMouseButton => InputType.KeyboardMouse,
			InputEventJoypadButton or InputEventJoypadMotion => InputType.Gamepad,
			_ => _currentInputType
		};
	}
}
