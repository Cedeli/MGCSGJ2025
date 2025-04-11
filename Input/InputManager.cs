using System;
using Godot;

public partial class InputManager : Node
{
	public enum InputType
	{
		KeyboardMouse,
		Gamepad,
	}

	public enum MovementMode
	{
		Grounded,
		Ship,
	}

	[Signal]
	public delegate void InputTypeChangedEventHandler(InputType newType);

	[Signal]
	public delegate void MovementModeChangedEventHandler(MovementMode newMode);

	[Export]
	private InputBuffer _inputBuffer;

	[Export]
	private KeyboardMouseInput _keyboardMouseInput;

	private InputType _currentInputType = InputType.KeyboardMouse;
	private MovementMode _currentMovementMode = MovementMode.Grounded;

	public MovementMode CurrentMovementMode => _currentMovementMode;

	public override void _Ready()
	{
		if (_keyboardMouseInput == null)
		{
			GD.PrintErr("InputManager Error: KeyboardMouseInput node not found or assigned");
			return;
		}

		ConnectInputReceivers();
		_keyboardMouseInput.SetInputBuffer(_inputBuffer);
	}

	private void ConnectInputReceivers()
	{
		GetTree().CallGroup("input_receivers", "SetInputManager", this);
		GetTree().CallGroup("input_receivers", "SetInputBuffer", _inputBuffer);

		if (_keyboardMouseInput != null)
		{
			foreach (var node in GetTree().GetNodesInGroup("input_receivers"))
			{
				if (node is IInputReceiver receiver)
				{
					if (
						!_keyboardMouseInput.IsConnected(
							InputProvider.SignalName.MoveInput,
							Callable.From<Vector2>(receiver.OnMoveInput)
						)
					)
					{
						_keyboardMouseInput.MoveInput += receiver.OnMoveInput;
					}
					if (
						!_keyboardMouseInput.IsConnected(
							InputProvider.SignalName.LookInput,
							Callable.From<Vector2>(receiver.OnLookInput)
						)
					)
					{
						_keyboardMouseInput.LookInput += receiver.OnLookInput;
					}

					if (receiver is Player player)
					{
						if (
							!_keyboardMouseInput.IsConnected(
								InputProvider.SignalName.ShootInput,
								Callable.From(player.OnShootInput)
							)
						)
						{
							_keyboardMouseInput.ShootInput += player.OnShootInput;
						}
						if (
							!_keyboardMouseInput.IsConnected(
								InputProvider.SignalName.ReloadInput,
								Callable.From(player.OnReloadInput)
							)
						)
						{
							_keyboardMouseInput.ReloadInput += player.OnReloadInput;
						}
					}
				}
			}
		}
	}

	public void ChangeMovementMode(MovementMode newMode)
	{
		if (newMode == _currentMovementMode)
			return;

		_currentMovementMode = newMode;
		EmitSignal(SignalName.MovementModeChanged, (int)_currentMovementMode);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		var newType = DetectInputType(@event);
		if (newType == _currentInputType)
			return;

		_currentInputType = newType;
		EmitSignal(SignalName.InputTypeChanged, (int)_currentInputType);
	}

	private InputType DetectInputType(InputEvent @event)
	{
		return @event switch
		{
			InputEventKey or InputEventMouseMotion or InputEventMouseButton =>
				InputType.KeyboardMouse,
			InputEventJoypadButton or InputEventJoypadMotion => InputType.Gamepad,
			_ => _currentInputType,
		};
	}
}
