using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class MiniMap : Control
{
	[ExportGroup("Configuration")]
	[Export]
	public float MapRange = 100.0f;

	[Export]
	public float MinMarkerAlpha = 0.3f;

	[Export]
	public float MaxMarkerAlpha = 1.0f;

	[Export]
	public float FullAlphaDistance = 15.0f;

	[Export]
	public float MinAlphaDistanceMultiplier = 1.5f;

	[ExportGroup("Node Paths")]
	[Export]
	public NodePath EnemyMarkerContainerPath { get; set; }

	[Export]
	public NodePath MapGridPath { get; set; }

	[ExportGroup("Prefabs")]
	[Export]
	public PackedScene PlayerMarkerScene { get; set; }

	[Export]
	public PackedScene ObjectiveMarkerScene { get; set; }

	[Export]
	public PackedScene EnemyMarkerScene { get; set; }

	private Player _player;
	private Camera3D _playerCamera;
	private Ship _objective;

	private Control _playerMarkerInstance;
	private Control _objectiveMarkerInstance;
	private Dictionary<Alien, Control> _enemyMarkers = new Dictionary<Alien, Control>();

	private Control _mapGrid;
	private Node _enemyMarkerContainer;

	private Vector2 _gridCenter;
	private float _gridMapScale;
	private float _minAlphaDistance;
	private bool _initialized = false;

	public override void _Ready()
	{
		CallDeferred(nameof(InitializeMapProperties));

		_mapGrid = GetNodeOrNull<Control>(MapGridPath);
		_enemyMarkerContainer = GetNodeOrNull<Node>(EnemyMarkerContainerPath);
	}

	private void InitializeMapProperties()
	{
		if (!IsInstanceValid(_mapGrid) || _mapGrid.Size.X <= 1 || _mapGrid.Size.Y <= 1)
		{
			GetTree().CreateTimer(0.1).Timeout += InitializeMapProperties;
			return;
		}
		if (_initialized)
			return;

		_gridCenter = _mapGrid.Size / 2.0f;
		if (MapRange <= 0)
			MapRange = 1.0f;
		_gridMapScale = Mathf.Min(_gridCenter.X, _gridCenter.Y) / MapRange;
		_minAlphaDistance = MapRange * MinAlphaDistanceMultiplier;

		_mapGrid.Rotation = 0;
		_mapGrid.PivotOffset = Vector2.Zero;
		_mapGrid.Position = Vector2.Zero;

		InstantiatePlayerMarker();
		InstantiateObjectiveMarker();
		FindTrackedNodes();

		_initialized = true;
		GD.Print(
			$"Minimap Initialized: GridCenter={_gridCenter}, GridScale={_gridMapScale}, MinAlphaDist={_minAlphaDistance}"
		);
	}

	private void InstantiatePlayerMarker()
	{
		if (_playerMarkerInstance != null && IsInstanceValid(_playerMarkerInstance))
			return;
		if (PlayerMarkerScene == null || _mapGrid == null)
			return;

		_playerMarkerInstance = PlayerMarkerScene.Instantiate<Control>();
		if (_playerMarkerInstance != null)
		{
			_mapGrid.AddChild(_playerMarkerInstance);

			Vector2 markerSize = _playerMarkerInstance.GetCombinedMinimumSize();
			if (markerSize == Vector2.Zero && _playerMarkerInstance is ColorRect cr)
				markerSize = cr.CustomMinimumSize;
			_playerMarkerInstance.PivotOffset = markerSize / 2.0f;

			_playerMarkerInstance.Position = _gridCenter - _playerMarkerInstance.PivotOffset;
			_playerMarkerInstance.ZIndex = 10;
			_playerMarkerInstance.Visible = false;
		}
		else
			GD.PrintErr("Minimap Error: Failed to instantiate PlayerMarkerScene.");
	}

	private void InstantiateObjectiveMarker()
	{
		if (_objectiveMarkerInstance != null && IsInstanceValid(_objectiveMarkerInstance))
			return;
		if (ObjectiveMarkerScene == null || _mapGrid == null)
			return;

		_objectiveMarkerInstance = ObjectiveMarkerScene.Instantiate<Control>();
		if (_objectiveMarkerInstance != null)
		{
			_mapGrid.AddChild(_objectiveMarkerInstance);
			_objectiveMarkerInstance.Visible = false;
			_objectiveMarkerInstance.ZIndex = 5;
		}
		else
			GD.PrintErr("Minimap Error: Failed to instantiate ObjectiveMarkerScene.");
	}

	public override void _Process(double delta)
	{
		if (!_initialized)
			return;

		if (!IsInstanceValid(_player) || !IsInstanceValid(_playerCamera))
		{
			FindTrackedNodes();
			if (!IsInstanceValid(_player) || !IsInstanceValid(_playerCamera))
			{
				if (_playerMarkerInstance != null)
					_playerMarkerInstance.Visible = false;
				if (_objectiveMarkerInstance != null)
					_objectiveMarkerInstance.Visible = false;
				foreach (var marker in _enemyMarkers.Values)
					if (IsInstanceValid(marker))
						marker.Visible = false;
				return;
			}
		}
		if (_playerMarkerInstance != null)
		{
			_playerMarkerInstance.Visible = true;
			_playerMarkerInstance.Position = _gridCenter - _playerMarkerInstance.PivotOffset;
		}

		if (!IsInstanceValid(_objectiveMarkerInstance))
			InstantiateObjectiveMarker();

		if (IsInstanceValid(_objective) && IsInstanceValid(_objectiveMarkerInstance))
		{
			UpdateMarkerPosition(
				_objectiveMarkerInstance,
				_objective.GlobalPosition,
				_playerCamera.GlobalPosition,
				false
			);
		}
		else
		{
			FindTrackedNodes();
			if (_objectiveMarkerInstance != null)
				_objectiveMarkerInstance.Visible = false;
		}
		UpdateEnemyMarkers();
	}

	private void FindTrackedNodes()
	{
		_player = GetNodeFromGroupHelper<Player>(Player.PlayerGroup);
		if (_player != null)
		{
			_playerCamera = _player.GetNodeOrNull<Camera3D>("Pivot/Camera3D");
		}
		else
		{
			_playerCamera = null;
		}
		_objective = GetNodeFromGroupHelper<Ship>(Ship.ShipGroup);
	}

	private void UpdateMarkerPosition(
		Control marker,
		Vector3 targetWorldPos,
		Vector3 referenceWorldPos,
		bool applyDistanceDimming = true
	)
	{
		if (marker == null || !IsInstanceValid(_player))
		{
			if (marker != null)
				marker.Visible = false;
			return;
		}

		Vector3 diffFromReference = targetWorldPos - referenceWorldPos;
		float distance3D_forDimming = targetWorldPos.DistanceTo(_player.GlobalPosition);

		Vector3 playerUp = -_player.GetGravityDirection().Normalized();
		if (playerUp.LengthSquared() < 0.01f)
			playerUp = _player.GlobalTransform.Basis.Y.Normalized();
		if (playerUp.LengthSquared() < 0.01f)
			playerUp = Vector3.Up;

		Vector3 diffOnPlane = diffFromReference - playerUp * diffFromReference.Dot(playerUp);

		Basis playerOrientationBasis = _player.GetCameraPivot().GlobalTransform.Basis;
		Vector3 playerForwardOnPlane = (
			-playerOrientationBasis.Z - playerUp * (-playerOrientationBasis.Z).Dot(playerUp)
		).Normalized();
		Vector3 playerRightOnPlane = playerUp.Cross(playerForwardOnPlane).Normalized();

		if (playerRightOnPlane.LengthSquared() < 0.1f) { }
		if (playerRightOnPlane.LengthSquared() < 0.1f)
		{
			Vector3 basisXOnPlane = (
				playerOrientationBasis.X - playerUp * playerOrientationBasis.X.Dot(playerUp)
			);
			if (basisXOnPlane.LengthSquared() > 0.1f)
			{
				playerRightOnPlane = basisXOnPlane.Normalized();
				playerForwardOnPlane = playerRightOnPlane.Cross(playerUp).Normalized();
			}
			else
			{
				playerRightOnPlane = (
					Vector3.Right - playerUp * Vector3.Right.Dot(playerUp)
				).Normalized();
				if (playerRightOnPlane.LengthSquared() < 0.1f)
					playerRightOnPlane = playerUp.Cross(Vector3.Forward).Normalized();
				playerForwardOnPlane = playerRightOnPlane.Cross(playerUp).Normalized();
			}
		}
		else
		{
			playerForwardOnPlane = playerRightOnPlane.Cross(playerUp).Normalized();
		}

		float localX = diffOnPlane.Dot(playerRightOnPlane);
		float localY = diffOnPlane.Dot(playerForwardOnPlane);

		Vector2 markerOffset = new Vector2(-localX * _gridMapScale, -localY * _gridMapScale);
		Vector2 targetPosOnMap = _gridCenter + markerOffset;

		// dimming
		float targetAlpha = MaxMarkerAlpha;
		if (applyDistanceDimming)
		{
			if (distance3D_forDimming <= FullAlphaDistance)
				targetAlpha = MaxMarkerAlpha;
			else if (distance3D_forDimming >= _minAlphaDistance)
				targetAlpha = MinMarkerAlpha;
			else
			{
				float t = Mathf.InverseLerp(
					FullAlphaDistance,
					_minAlphaDistance,
					distance3D_forDimming
				);
				targetAlpha = Mathf.Lerp(MaxMarkerAlpha, MinMarkerAlpha, t);
			}
		}

		float maxDist = _gridMapScale * MapRange * 0.98f;
		Color currentModulate = marker.Modulate;
		Vector2 finalMarkerPos;

		if (markerOffset.LengthSquared() > maxDist * maxDist)
		{
			Vector2 dir = markerOffset.Normalized();
			finalMarkerPos = _gridCenter + dir * maxDist;
			currentModulate.A = Mathf.Min(targetAlpha, 0.4f);
		}
		else
		{
			finalMarkerPos = targetPosOnMap;
			currentModulate.A = targetAlpha;
		}
		marker.Modulate = currentModulate;

		Vector2 markerSize = marker.GetCombinedMinimumSize();
		if (markerSize == Vector2.Zero && marker is ColorRect cr)
			markerSize = cr.CustomMinimumSize;
		if (marker.PivotOffset != markerSize / 2.0f)
		{
			marker.PivotOffset = markerSize / 2.0f;
		}

		marker.Position = finalMarkerPos - marker.PivotOffset;
		marker.Visible = true;
	}

	private void UpdateEnemyMarkers()
	{
		if (
			_enemyMarkerContainer == null
			|| EnemyMarkerScene == null
			|| !IsInstanceValid(_player)
			|| !IsInstanceValid(_playerCamera)
		)
			return;
		Vector3 cameraPos = _playerCamera.GlobalPosition;

		var currentAliens = GetTree()
			.GetNodesInGroup("alien")
			.Cast<Alien>()
			.Where(a => IsInstanceValid(a))
			.ToList();
		var trackedAliens = _enemyMarkers.Keys.ToList();

		foreach (Alien alien in trackedAliens) { }
		foreach (Alien alien in trackedAliens)
		{
			if (!IsInstanceValid(alien) || !currentAliens.Contains(alien))
			{
				if (_enemyMarkers.TryGetValue(alien, out Control marker) && IsInstanceValid(marker))
					marker.QueueFree();
				_enemyMarkers.Remove(alien);
			}
		}

		foreach (Alien alien in currentAliens)
		{
			if (_enemyMarkers.TryGetValue(alien, out Control marker))
			{
				if (IsInstanceValid(marker))
					UpdateMarkerPosition(marker, alien.GlobalPosition, cameraPos, true);
				else
				{
					_enemyMarkers.Remove(alien);
					CreateEnemyMarker(alien);
				}
			}
			else
				CreateEnemyMarker(alien);
		}
	}

	private void CreateEnemyMarker(Alien alien)
	{
		if (
			_enemyMarkerContainer == null
			|| EnemyMarkerScene == null
			|| !IsInstanceValid(alien)
			|| !IsInstanceValid(_playerCamera)
		)
			return;
		Control newMarker = EnemyMarkerScene.Instantiate<Control>();
		if (newMarker != null)
		{
			_enemyMarkerContainer.AddChild(newMarker);
			newMarker.ZIndex = 1;
			_enemyMarkers[alien] = newMarker;
			UpdateMarkerPosition(
				newMarker,
				alien.GlobalPosition,
				_playerCamera.GlobalPosition,
				true
			);
		}
		else
			GD.PrintErr("Minimap Error: Failed to instantiate EnemyMarkerScene.");
	}

	private T GetNodeFromGroupHelper<T>(string group)
		where T : Node
	{
		var nodes = GetTree().GetNodesInGroup(group);
		if (nodes.Count > 0 && nodes[0] is T typedNode && IsInstanceValid(typedNode))
			return typedNode;
		return null;
	}
}
