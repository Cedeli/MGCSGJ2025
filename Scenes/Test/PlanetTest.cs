using System;
using Godot;

public partial class PlanetTest : Node3D
{
	[Export]
	private NodePath planetNodePath;

	[Export]
	private int polesLatitudeCount = 10;

	[Export]
	private int polesLongitudeCount = 20;

	[Export]
	private float poleHeight = 1.0f;

	[Export]
	private float poleRadius = 0.1f;

	[Export]
	private Color poleColor = Colors.Red;

	private Planet _planet;
	private StandardMaterial3D _poleMaterial;
	private Timer _regenerationTimer;
	private Node3D _polesContainer;

	public override void _Ready()
	{
		if (planetNodePath == null || GetNodeOrNull(planetNodePath) == null)
		{
			GD.PrintErr("Skipping init");
			return;
		}

		_planet = GetNodeOrNull<Planet>(planetNodePath);
		if (_planet == null)
		{
			GD.PrintErr("Skipping init");
			return;
		}

		_polesContainer = new Node3D();
		_polesContainer.Name = "PolesContainer";
		_planet.AddChild(_polesContainer);

		_regenerationTimer = new Timer();
		_regenerationTimer.Name = "RegenerationTimer";
		_regenerationTimer.WaitTime = 0.5;
		_regenerationTimer.Autostart = true;
		_regenerationTimer.OneShot = false;

		AddChild(_regenerationTimer);

		if (
			!_regenerationTimer.IsConnected(Timer.SignalName.Timeout, Callable.From(OnTimerTimeout))
		)
		{
			Error err = _regenerationTimer.Connect(
				Timer.SignalName.Timeout,
				Callable.From(OnTimerTimeout)
			);
			if (err != Error.Ok)
			{
				GD.PrintErr($"Failed to connect timer signal Error: {err}");
				return;
			}
		}
		_regenerationTimer.Start();

		CallDeferred(nameof(PlacePoles));
	}

	private void OnTimerTimeout()
	{
		if (_planet != null && IsInstanceValid(_planet))
		{
			_planet.Regenerate();
			CallDeferred(nameof(PlacePoles));
		}
		else
		{
			GD.PrintErr("Planet instance is null or invalid in OnTimerTimeout, Stopping timer.");
			_regenerationTimer?.Stop();
		}
	}

	private void PlacePoles()
	{
		if (_planet == null || _polesContainer == null)
		{
			GD.Print("Skipping pole placement as Planet or PolesContainer node  not available.");
			return;
		}

		foreach (Node child in _polesContainer.GetChildren())
		{
			child.QueueFree();
		}

		if (_poleMaterial == null)
		{
			_poleMaterial = new StandardMaterial3D { AlbedoColor = poleColor, Roughness = 0.8f };
		}

		var poleMesh = new CylinderMesh
		{
			TopRadius = poleRadius,
			BottomRadius = poleRadius,
			Height = poleHeight,
		};

		int polesPlaced = 0;
		for (int i = 1; i < polesLatitudeCount; i++)
		{
			float v = (float)i / polesLatitudeCount;
			for (int j = 0; j < polesLongitudeCount; j++)
			{
				float u = (float)j / polesLongitudeCount;
				Vector2 uv = new Vector2(u, v);

				Vector3 surfacePositionLocal = _planet.GetSurfacePosition(uv);
				Vector3 surfaceNormal = (
					surfacePositionLocal / surfacePositionLocal.Length()
				).Normalized();

				var poleInstance = new MeshInstance3D
				{
					Name = $"Pole_{i}_{j}",
					Mesh = poleMesh,
					MaterialOverride = _poleMaterial,
				};

				_polesContainer.AddChild(poleInstance);
				poleInstance.Position = surfacePositionLocal + surfaceNormal * (poleHeight / 2.0f);

				Vector3 up = surfaceNormal;
				Vector3 tempForward = Vector3.Forward;
				if (Mathf.Abs(up.Dot(tempForward)) > 0.99f)
				{
					tempForward = Vector3.Right;
				}
				Vector3 right = up.Cross(tempForward).Normalized();
				Vector3 forward = right.Cross(up).Normalized();

				Basis poleBasis = new Basis(right, up, -forward).Orthonormalized();
				poleInstance.Basis = poleBasis;

				polesPlaced++;
			}
		}
	}

	public override void _ExitTree()
	{
		if (_regenerationTimer != null)
		{
			_regenerationTimer.Stop();
			if (
				_regenerationTimer.IsConnected(
					Timer.SignalName.Timeout,
					Callable.From(OnTimerTimeout)
				)
			)
			{
				_regenerationTimer.Timeout -= OnTimerTimeout;
			}
		}
	}
}
