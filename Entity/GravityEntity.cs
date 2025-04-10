using System;
using System.Collections.Generic;
using Godot;

public partial class GravityEntity : RigidBody3D
{
	[ExportGroup("Movement")]
	[Export]
	public float MoveSpeed = 5.0f;

	[Export]
	public float JumpImpulse = 10.0f;

	[ExportGroup("Components")]
	[Export]
	protected ShapeCast3D GroundCast;

	[Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
	protected float GroundCastLength = 0.5f;

	private const float AngularOrientationSpeed = 0.5f;
	private const float GravityDirectionLerpSpeed = 5.0f;
	private const float GroundOrientationLerpSpeed = 10.0f;
	private const float AirOrientationLerpSpeed = 3.0f;

	private const float GravityInfluenceRadiusMultiplier = 5.0f;
	private const float CelestialBodyScanInterval = 1.0f;
	private const float GravityThreshold = 0.001f;

	private CelestialBody _currentGravitySource;
	private Vector3 _currentGravityDirection = Vector3.Down;
	private Vector3 _targetGravityDirection = Vector3.Down;
	private float _effectiveGravityMagnitude;
	private bool _isGrounded;
	private float _celestialBodyScanTimer;
	private readonly List<CelestialBody> _nearbyCelestialBodies = [];

	public override void _Ready()
	{
		ConfigureRigidBody();
		UpdateGroundCastTarget();
	}

	public override void _PhysicsProcess(double delta)
	{
		var fDelta = (float)delta;
		UpdateGroundCastTarget();
		UpdateGravitySource(fDelta);
		UpdateGravityState(fDelta);
		UpdateOrientation(fDelta);
		ApplyGravity();
	}

	private void ConfigureRigidBody()
	{
		ContinuousCd = true;
		ContactMonitor = true;
		MaxContactsReported = 4;

		LinearDamp = 0.1f;
		AngularDamp = 0.8f;

		AxisLockLinearX = false;
		AxisLockLinearY = false;
		AxisLockLinearZ = false;
		AxisLockAngularX = false;
		AxisLockAngularY = false;
		AxisLockAngularZ = false;
	}

	private void UpdateGroundCastTarget()
	{
		var globalDownDirection = GetGravityDirection();
		globalDownDirection = globalDownDirection.Normalized();

		var globalTargetPoint = GroundCast.GlobalPosition + globalDownDirection * GroundCastLength;
		GroundCast.TargetPosition = GroundCast.ToLocal(globalTargetPoint);
		_isGrounded = GroundCast.IsColliding();
	}

	private void UpdateGravitySource(float delta)
	{
		_celestialBodyScanTimer += delta;
		if (_celestialBodyScanTimer >= CelestialBodyScanInterval)
		{
			_celestialBodyScanTimer = 0f;
			ScanForCelestialBodies();
		}

		_currentGravitySource = FindClosestCelestialBody();
	}

	private void ScanForCelestialBodies()
	{
		_nearbyCelestialBodies.Clear();
		var bodies = GetTree().GetNodesInGroup("celestial_bodies");
		foreach (var node in bodies)
		{
			if (node is CelestialBody celestialBody)
			{
				_nearbyCelestialBodies.Add(celestialBody);
			}
		}
	}

	private CelestialBody FindClosestCelestialBody()
	{
		CelestialBody closestBody = null;
		var closestDistanceSqr = float.MaxValue;
		var entityPos = GlobalPosition;

		foreach (var celestialBody in _nearbyCelestialBodies)
		{
			if (celestialBody == null || !IsInstanceValid(celestialBody))
				continue;

			var distanceSqr = entityPos.DistanceSquaredTo(celestialBody.GlobalPosition);
			var influenceRadius = celestialBody.Radius * GravityInfluenceRadiusMultiplier;
			var influenceRadiusSqr = influenceRadius * influenceRadius;

			if (!(distanceSqr < influenceRadiusSqr) || !(distanceSqr < closestDistanceSqr))
				continue;
			closestDistanceSqr = distanceSqr;
			closestBody = celestialBody;
		}

		return closestBody;
	}

	private void UpdateGravityState(float delta)
	{
		if (_currentGravitySource != null && IsInstanceValid(_currentGravitySource))
		{
			_targetGravityDirection = (
				_currentGravitySource.GlobalPosition - GlobalPosition
			).Normalized();
			var surfaceGravity = _currentGravitySource.SurfaceGravity;
			var distanceFalloff = CalculateGravityFalloff();
			_effectiveGravityMagnitude = surfaceGravity * distanceFalloff;
		}
		else
		{
			_targetGravityDirection = Vector3.Zero;
			_effectiveGravityMagnitude = 0f;
		}

		_currentGravityDirection = _currentGravityDirection
			.Lerp(_targetGravityDirection, delta * GravityDirectionLerpSpeed)
			.Normalized();
	}

	private float CalculateGravityFalloff()
	{
		if (_currentGravitySource == null || !IsInstanceValid(_currentGravitySource))
			return 0f;

		var distance = GlobalPosition.DistanceTo(_currentGravitySource.GlobalPosition);
		var surfaceRadius = _currentGravitySource.Radius;

		if (distance < 0.01f)
			return 1.0f;

		return distance <= surfaceRadius ? 1.0f : Mathf.Pow(surfaceRadius / distance, 2);
	}

	private void UpdateOrientation(float delta)
	{
		if (_effectiveGravityMagnitude < GravityThreshold)
		{
			AngularDamp = 1.0f;
			return;
		}

		var entityUp = -_currentGravityDirection;
		var targetBasis = CreateBasisFromUp(entityUp, Basis.Z);

		var currentQuat = GlobalTransform.Basis.GetRotationQuaternion();
		var targetQuat = targetBasis.GetRotationQuaternion().Normalized();

		var lerpSpeed = _isGrounded ? GroundOrientationLerpSpeed : AirOrientationLerpSpeed;
		var weight = Mathf.Clamp(delta * lerpSpeed, 0.0f, 1.0f);

		var interpolatedQuat = currentQuat.Slerp(targetQuat, weight);
		var rotationDiff = interpolatedQuat * currentQuat.Inverse();
		var angle = rotationDiff.GetAngle();

		if (Mathf.Abs(angle) > Mathf.Epsilon && delta > 0.0f)
		{
			var axis = rotationDiff.GetAxis();
			axis = axis.Normalized();

			AngularVelocity = axis * (angle / delta) * AngularOrientationSpeed;
		}
		else
		{
			AngularVelocity = AngularVelocity.Lerp(Vector3.Zero, delta * 5.0f);
		}

		AngularDamp = 0.1f;
	}

	protected static Basis CreateBasisFromUp(Vector3 up, Vector3 currentForwardHint)
	{
		up = up.Normalized();

		var forward = (currentForwardHint - up * up.Dot(currentForwardHint)).Normalized();

		if (forward.LengthSquared() < 0.001f)
		{
			forward =
				Mathf.Abs(up.Dot(Vector3.Forward)) > 0.999f
					? (Vector3.Right - up * up.Dot(Vector3.Right)).Normalized()
					: (Vector3.Forward - up * up.Dot(Vector3.Forward)).Normalized();
		}

		var right = up.Cross(forward).Normalized();
		var basisZ = right.Cross(up);

		return new Basis(right, up, basisZ);
	}

	private void ApplyGravity()
	{
		if (_effectiveGravityMagnitude > GravityThreshold)
		{
			ApplyCentralForce(_currentGravityDirection * _effectiveGravityMagnitude * Mass);
		}
	}

	public Vector3 GetGravityDirection()
	{
		return _currentGravityDirection;
	}

	public bool IsGrounded()
	{
		return _isGrounded;
	}
}
