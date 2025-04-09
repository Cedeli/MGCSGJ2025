using Godot;
using System;

public abstract class MovementController(Player player)
{
    protected readonly Player PlayerBody = player;

    public abstract void PhysicsProcess(float delta, Vector2 rawMovementInput);
    public abstract bool TryJump();
}

internal class PlanetaryMovementController(Player player) : MovementController(player)
{
    private const float GroundFrictionFactor = 10.0f;
    private const float AirControlFactor = 0.5f;
    private const float InputThresholdSq = 0.01f;

    public override void PhysicsProcess(float delta, Vector2 rawMovementInput)
    {
        HandleMovement(delta, rawMovementInput);
    }

    private void HandleMovement(float delta, Vector2 rawMovementInput)
    {
        var targetVelocity = CalculateTargetVelocity(rawMovementInput);
    
        var currentVelocity = PlayerBody.LinearVelocity;
        var verticalVelocity = currentVelocity.Project(PlayerBody.GetGravityDirection());
        var horizontalVelocity = currentVelocity - verticalVelocity;
    
        if (PlayerBody.IsGrounded())
        {
            ApplyGroundedMovement(delta, targetVelocity, horizontalVelocity, verticalVelocity);
        }
        else
        {
            ApplyAerialMovement(delta, targetVelocity, horizontalVelocity);
        }
    }

    private Vector3 CalculateTargetVelocity(Vector2 rawMovementInput)
    {
        var pivot = PlayerBody.GetCameraPivot();
        if (pivot == null || rawMovementInput == Vector2.Zero)
        {
            return Vector3.Zero;
        }
    
        var pivotBasis = pivot.GlobalTransform.Basis;
        var camForward = -pivotBasis.Z;
        var camRight = pivotBasis.X;
    
        var desiredMoveDir = (camRight * rawMovementInput.X + camForward * -rawMovementInput.Y).Normalized();
    
        var playerUp = -PlayerBody.GetGravityDirection();
        var moveOnPlane = (desiredMoveDir - playerUp * desiredMoveDir.Dot(playerUp)).Normalized();
    
        return moveOnPlane * PlayerBody.MoveSpeed;
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

    public override bool TryJump()
    {
        if (!PlayerBody.IsGrounded())
            return false;

        var jumpDirection = -PlayerBody.GetGravityDirection();
        PlayerBody.ApplyCentralImpulse(jumpDirection * PlayerBody.JumpImpulse);
        return true;
    }
}