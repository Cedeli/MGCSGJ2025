using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class InputBuffer : Node
{
	[Export] public float BufferTimeWindow = 0.2f;
	
	private Dictionary<string, BufferedAction> _bufferedActions = new();
	
	public class BufferedAction
	{
		public readonly double Timestamp;
		public bool Consumed;
		public Vector2? Direction;
		
		public BufferedAction(double timestamp, Vector2? direction = null)
		{
			Timestamp = timestamp;
			Consumed = false;
			Direction = direction;
		}
	}
	
	public override void _Process(double delta)
	{
		CleanupExpiredActions();
	}
	
	private void CleanupExpiredActions()
	{
		var currentTime = Time.GetTicksMsec() / 1000.0;
		var expiredActions = _bufferedActions
			.Where(action => currentTime - action.Value.Timestamp > BufferTimeWindow || action.Value.Consumed)
			.Select(action => action.Key)
			.ToList();
			
		foreach (var action in expiredActions)
		{
			_bufferedActions.Remove(action);
		}
	}
	
	public void BufferAction(string actionName, Vector2? direction = null)
	{
		_bufferedActions[actionName] = new BufferedAction(Time.GetTicksMsec() / 1000.0, direction);
	}
	
	public bool ConsumeBufferedAction(string actionName, out Vector2? direction)
	{
		direction = null;
		
		if (!_bufferedActions.TryGetValue(actionName, out var action) || action.Consumed)
			return false;
			
		action.Consumed = true;
		direction = action.Direction;
		return true;
	}
	
	public bool ConsumeBufferedAction(string actionName)
	{
		return ConsumeBufferedAction(actionName, out _);
	}
	
	public bool HasBufferedAction(string actionName)
	{
		return _bufferedActions.ContainsKey(actionName) && !_bufferedActions[actionName].Consumed;
	}
	
	public void ClearBuffer()
	{
		_bufferedActions.Clear();
	}
}
