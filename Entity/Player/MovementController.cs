using Godot;
using System;
using System.Collections.Generic;

public abstract class MovementController
{
    protected RigidBody3D PlayerBody;
    protected float MoveSpeed;

    protected MovementController(RigidBody3D playerBody, float moveSpeed)
    {
        PlayerBody = playerBody;
        MoveSpeed = moveSpeed;
    }

    public abstract void PhysicsProcess(float delta, Vector2 rawMovementInput);
    public abstract bool TryJump();
    public abstract Vector3 GetGravityDirection();
}

public class PlanetaryMovementController : MovementController
{
    private const float AngularOrientationSpeed = 0.5f;
    private const float GravityDirectionLerpSpeed = 5.0f;
    private const float GroundOrientationLerpSpeed = 10.0f;
    private const float AirOrientationLerpSpeed = 3.0f;
    private const float GroundFrictionFactor = 10.0f;
    private const float AirControlFactor = 0.5f;

    private const float GravityInfluenceRadiusMultiplier = 5.0f;

    private const float CelestialBodyScanInterval = 1.0f;
    private const float InputThresholdSq = 0.01f;

    private const float GravityThreshold = 0.001f;

    private readonly ShapeCast3D _groundCast;
    private readonly Node3D _cameraPivot;
    private readonly float _jumpImpulse;

    private CelestialBody _currentGravitySource;
    private Vector3 _currentGravityDirection = Vector3.Down;
    private Vector3 _targetGravityDirection = Vector3.Down;
    private float _effectiveGravityMagnitude;
    private bool _isGrounded;
    private float _celestialBodyScanTimer;
    private readonly List<CelestialBody> _nearbyCelestialBodies = [];

    public PlanetaryMovementController(
        RigidBody3D player,
        ShapeCast3D groundCast,
        Node3D cameraPivot,
        float moveSpeed,
        float jumpImpulse)
        : base(player, moveSpeed)
    {
        ArgumentNullException.ThrowIfNull(groundCast);
        ArgumentNullException.ThrowIfNull(cameraPivot);

        _groundCast = groundCast;
        _cameraPivot = cameraPivot;
        _jumpImpulse = jumpImpulse;

        ScanForCelestialBodies();
    }

    public override void PhysicsProcess(float delta, Vector2 rawMovementInput)
    {
        _isGrounded = CheckGrounded();

        UpdateGravitySource(delta);
        UpdateGravityState(delta);
        UpdateOrientation(delta);
        HandleMovement(delta, rawMovementInput);
        ApplyGravity();
    }

    private bool CheckGrounded()
    {
        return _groundCast.IsColliding();
    }

    private void UpdateGravitySource(float delta)
    {
        _celestialBodyScanTimer += delta;
        if (_celestialBodyScanTimer >= CelestialBodyScanInterval)
        {
            _celestialBodyScanTimer = 0f;
            ScanForCelestialBodies();
        }

        _currentGravitySource = FindClosestCelestialBodyFromCache();
    }

    private void ScanForCelestialBodies()
    {
        _nearbyCelestialBodies.Clear();
        var bodies = PlayerBody.GetTree().GetNodesInGroup("celestial_bodies");
        foreach (var node in bodies)
        {
            if (node is CelestialBody celestialBody)
            {
                _nearbyCelestialBodies.Add(celestialBody);
            }
        }
    }

    private CelestialBody FindClosestCelestialBodyFromCache()
    {
        CelestialBody closestBody = null;
        var closestDistanceSqr = float.MaxValue;
        var playerPos = PlayerBody.GlobalPosition;

        foreach (var celestialBody in _nearbyCelestialBodies)
        {
            if (celestialBody == null || !GodotObject.IsInstanceValid(celestialBody))
                continue;

            var distanceSqr = playerPos.DistanceSquaredTo(celestialBody.GlobalPosition);
            var influenceRadius = celestialBody.Radius * GravityInfluenceRadiusMultiplier;
            var influenceRadiusSqr = influenceRadius * influenceRadius;

            if (!(distanceSqr < influenceRadiusSqr) || !(distanceSqr < closestDistanceSqr)) continue;
            closestDistanceSqr = distanceSqr;
            closestBody = celestialBody;
        }

        return closestBody;
    }

    private void UpdateGravityState(float delta)
    {
        if (_currentGravitySource != null && GodotObject.IsInstanceValid(_currentGravitySource))
        {
            _targetGravityDirection = (_currentGravitySource.GlobalPosition - PlayerBody.GlobalPosition).Normalized();
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
            .Lerp(_targetGravityDirection, delta * GravityDirectionLerpSpeed).Normalized();
    }

    private float CalculateGravityFalloff()
    {
        if (_currentGravitySource == null || !GodotObject.IsInstanceValid(_currentGravitySource))
            return 0f;

        var distance = PlayerBody.GlobalPosition.DistanceTo(_currentGravitySource.GlobalPosition);
        var surfaceRadius = _currentGravitySource.Radius;

        if (distance < 0.01f) return 1.0f;

        return distance <= surfaceRadius ? 1.0f : Mathf.Pow(surfaceRadius / distance, 2);
    }

    private void UpdateOrientation(float delta)
    {
        if (_effectiveGravityMagnitude < GravityThreshold)
        {
            PlayerBody.AngularDamp = 1.0f;
            return;
        }

        var playerUp = -_currentGravityDirection;
        var targetBasis = CreateBasisFromUp(playerUp, PlayerBody.Basis.Z);

        var currentQuat = PlayerBody.GlobalTransform.Basis.GetRotationQuaternion();
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

            PlayerBody.AngularVelocity = axis * (angle / delta) * AngularOrientationSpeed;
        }
        else
        {
            PlayerBody.AngularVelocity = PlayerBody.AngularVelocity.Lerp(Vector3.Zero, delta * 5.0f);
        }

        PlayerBody.AngularDamp = 0.1f;
    }

    public static Basis CreateBasisFromUp(Vector3 up, Vector3 currentForwardHint)
    {
        up = up.Normalized();

        var forward = (currentForwardHint - up * up.Dot(currentForwardHint)).Normalized();

        if (forward.LengthSquared() < 0.001f)
        {
            forward = Mathf.Abs(up.Dot(Vector3.Forward)) > 0.999f
                ? (Vector3.Right - up * up.Dot(Vector3.Right)).Normalized()
                : (Vector3.Forward - up * up.Dot(Vector3.Forward)).Normalized();
        }

        var right = up.Cross(forward).Normalized();
        var basisZ = right.Cross(up);

        return new Basis(right, up, basisZ);
    }

    private void HandleMovement(float delta, Vector2 rawMovementInput)
    {
        var targetVelocity = CalculateTargetHorizontalVelocity(rawMovementInput);

        var currentVelocity = PlayerBody.LinearVelocity;
        var verticalVelocity = currentVelocity.Project(_currentGravityDirection);
        var horizontalVelocity = currentVelocity - verticalVelocity;

        if (_isGrounded)
        {
            ApplyGroundedMovement(delta, targetVelocity, horizontalVelocity, verticalVelocity);
        }
        else
        {
            ApplyAerialMovement(delta, targetVelocity, horizontalVelocity);
        }
    }

    private Vector3 CalculateTargetHorizontalVelocity(Vector2 rawMovementInput)
    {
        if (_cameraPivot == null || rawMovementInput == Vector2.Zero)
        {
            return Vector3.Zero;
        }

        var pivotBasis = _cameraPivot.GlobalTransform.Basis;
        var camForward = -pivotBasis.Z;
        var camRight = pivotBasis.X;

        var desiredMoveDir = (camRight * rawMovementInput.X + camForward * -rawMovementInput.Y).Normalized();

        var playerUp = -_currentGravityDirection;
        var moveOnPlane = (desiredMoveDir - playerUp * desiredMoveDir.Dot(playerUp)).Normalized();

        return moveOnPlane * MoveSpeed;
    }

    private void ApplyGroundedMovement(float delta, Vector3 targetVelocity, Vector3 currentHorizontalVelocity,
        Vector3 currentVerticalVelocity)
    {
        if (targetVelocity.LengthSquared() > InputThresholdSq)
        {
            PlayerBody.LinearVelocity = targetVelocity + currentVerticalVelocity;
        }
        else
        {
            var dampenedHorizontal = currentHorizontalVelocity.Lerp(Vector3.Zero, GroundFrictionFactor * delta);
            PlayerBody.LinearVelocity = dampenedHorizontal + currentVerticalVelocity;
        }
    }

    private void ApplyAerialMovement(float delta, Vector3 targetVelocity, Vector3 currentHorizontalVelocity)
    {
        if (delta <= 0) return;

        var velocityChange = targetVelocity - currentHorizontalVelocity;
        var requiredAcceleration = velocityChange / delta;
        var airForce = requiredAcceleration * PlayerBody.Mass * AirControlFactor;

        PlayerBody.ApplyCentralForce(airForce);
    }

    private void ApplyGravity()
    {
        if (_effectiveGravityMagnitude > GravityThreshold)
        {
            PlayerBody.ApplyCentralForce(_currentGravityDirection * _effectiveGravityMagnitude * PlayerBody.Mass);
        }
    }

    public override bool TryJump()
    {
        if (!CheckGrounded())
            return false;

        var jumpDirection = -_currentGravityDirection;
        PlayerBody.ApplyCentralImpulse(jumpDirection * _jumpImpulse);
        return true;
    }

    public override Vector3 GetGravityDirection()
    {
        return _currentGravityDirection;
    }
}