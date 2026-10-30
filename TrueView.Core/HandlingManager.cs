using Comfort.Common;
using EFT;
using EFT.Animations;
using TrueView.Configuration;
using TrueView.Systems;
using UnityEngine;

namespace TrueView.Core;

internal static class HandlingManager
{
    private static int _lastFrame = -1;

    private static bool _wasActive;

    private static bool _lastLeftStance;

    // Track free-look state to detect the transition out of free look
    private static bool _wasFreeLooking;

    // Free-look return smoothing state — smoothly interpolates the camera
    // from the free-look end position back to the forward view instead of
    // snapping instantly.
    private static bool _freeLookReturnActive;
    private static Vector3 _freeLookReturnStart;
    private static Vector3 _freeLookReturnTarget;
    private static float _freeLookReturnT;

    // Track the current firearm instance so we can detect weapon swaps
    // and reset procedural state — prevents the weapon from being
    // misplaced when switching weapons mid-sprint.
    private static object _lastFirearm;

    // Whether the free-look return smoothing is currently active.
    // Patch_SetHeadRotation checks this to skip deadzone modifications
    // during the smoothing transition.
    public static bool IsFreeLookReturnActive => _freeLookReturnActive;

    public static void Tick(Player player, float dt)
    {
        if (player == null || !player.IsYourPlayer)
        {
            return;
        }
        if (player.MouseLookControl)
        {
            // Reset procedural state when entering free look so the camera
            // doesn't jump when free look ends and the deadzone re-engages.
            if (_wasActive)
            {
                ResetAll(player);
            }
            _wasFreeLooking = true;
            return;
        }

        // Just exited free look — start smoothing the camera back to the
        // forward rotation instead of snapping instantly.
        if (_wasFreeLooking)
        {
            _wasFreeLooking = false;
            ResetAll(player);

            // Capture the current camera rotation as the smoothing start point,
            // and the player's forward rotation as the target.
            Vector3 forwardRot = player.Rotation;
            _freeLookReturnStart = HandlingState.HeadRotation;
            _freeLookReturnTarget = forwardRot;
            _freeLookReturnT = 0f;

            float returnSpeed = HandlingConfig.FreeLookReturnSpeed?.Value ?? 12f;
            if (returnSpeed <= 0f)
            {
                // Instant snap — no smoothing
                if (player.ProceduralWeaponAnimation != null)
                {
                    player.ProceduralWeaponAnimation.SetHeadRotation(forwardRot);
                }
                HandlingState.HeadRotation = forwardRot;
            }
            else
            {
                _freeLookReturnActive = true;
            }
        }

        // If free-look return smoothing is active, interpolate the camera
        // towards the forward rotation and bypass normal deadzone processing.
        if (_freeLookReturnActive)
        {
            float returnSpeed = HandlingConfig.FreeLookReturnSpeed?.Value ?? 12f;
            _freeLookReturnT = Mathf.Clamp01(_freeLookReturnT + Time.deltaTime * returnSpeed);

            Vector3 smoothed = new Vector3(
                Mathf.LerpAngle(_freeLookReturnStart.x, _freeLookReturnTarget.x, _freeLookReturnT),
                Mathf.LerpAngle(_freeLookReturnStart.y, _freeLookReturnTarget.y, _freeLookReturnT),
                Mathf.LerpAngle(_freeLookReturnStart.z, _freeLookReturnTarget.z, _freeLookReturnT)
            );

            HandlingState.HeadRotation = smoothed;
            if (player.ProceduralWeaponAnimation != null)
            {
                player.ProceduralWeaponAnimation.SetHeadRotation(smoothed);
            }

            if (_freeLookReturnT >= 1f)
            {
                _freeLookReturnActive = false;
                // Deadzone systems were reset when free look ended and haven't
                // been modified during smoothing. They will re-initialize from
                // the current rotation on the next frame's normal processing.
                HandlingState.HeadRotation = _freeLookReturnTarget;
            }
            return;
        }
        // Reset all procedural state when inventory/looting is open so the
        // weapon position doesn't get corrupted by stale offsets.
        if (player.IsInventoryOpened)
        {
            if (_wasActive)
            {
                ResetAll(player);
            }
            return;
        }
        // Reset hip deadzone when shoulder swap changes so the weapon
        // doesn't float off the hands due to stale offsets.
        bool currentLeftStance = false;
        try
        {
            currentLeftStance = player.MovementContext?.LeftStanceController?.LeftStance ?? false;
        }
        catch { }
        if (currentLeftStance != _lastLeftStance)
        {
            ProceduralHipSystem.Reset();
            _lastLeftStance = currentLeftStance;
        }
        if (!HandlingState.Enabled)
        {
            if (_wasActive)
            {
                ResetAll(player);
            }
        }
        else if (_lastFrame != Time.frameCount)
        {
            _lastFrame = Time.frameCount;
            _wasActive = true;
            HandlingState.Update(player);

            bool handsTransition = false;
            try { handsTransition = player.HandsController?.IsHandsProcessing() ?? false; } catch { }
            ProceduralHipSystem.SetHandsTransition(handsTransition);

            // Detect weapon swap — reset procedural state so the new weapon
            // starts from a clean position instead of inheriting stale
            // offsets from the previous weapon. This fixes pistols (and
            // any weapon) being misplaced when equipped mid-sprint.
            object currentFirearm = HandlingState.Firearm;
            if (currentFirearm != _lastFirearm)
            {
                _lastFirearm = currentFirearm;
                ProceduralHipSystem.Reset();
                ProceduralHipSystem.SuppressCameraFollow(1.5f);
                SwaySystem.Reset();
                CameraLagSystem.Reset(player.ProceduralWeaponAnimation);
            }

            MotionTracker.Update(player, dt);
            InjuryHandlingSystem.Tick(player, dt);
            LocomotionGaitSystem.Tick(player, dt);
            TurnInPlaceSystem.Tick(player, dt);
            DynamicFovSystem.Tick(player, dt);
            AdrenalineSystem.Tick(player, dt);
            BreathingSystem.Tick(player, dt);
            SprintCancelSystem.Tick(player);
            if (HandlingState.HasWeapon)
            {
                AimingSystem.Apply();
                SwaySystem.Apply();
            }
            ProceduralHipSystem.Tick(player, dt);
            MomentumSystem.Tick(player, dt);
            LeanSystem.Tick(player, dt);
            TurnRollSystem.Tick(player, dt);
            if (ProceduralHipSystem.ControlsCamera)
                CameraLagSystem.Reset(player.ProceduralWeaponAnimation);
            else
                CameraLagSystem.Tick(dt);
            FluidMovementSystem.Tick(player, dt);
        }
    }

    public static void OnWeaponVariablesUpdated(Player player)
    {
        if (!(player == null) && player.IsYourPlayer && HandlingState.Enabled)
        {
            HandlingState.Update(player);
            HandlingState.RefreshStats();
            ShotSystem.ApplyWeaponVariables(player.ProceduralWeaponAnimation);
            AimingSystem.Apply();
        }
    }

    public static void ResetAll(Player player)
    {
        _wasActive = false;
        _wasFreeLooking = false;
        _freeLookReturnActive = false;
        _lastFirearm = null;
        SwaySystem.Reset();
        ShotSystem.Reset();
        CameraLagSystem.Reset(player?.ProceduralWeaponAnimation);
        LeanSystem.Reset();
        TurnRollSystem.Reset();
        MomentumSystem.Reset();
        LocomotionGaitSystem.Reset();
        TurnInPlaceSystem.Reset();
        DynamicFovSystem.Reset();
        AdrenalineSystem.Reset();
        BreathingSystem.Reset();
        InjuryHandlingSystem.Reset();
        MotionTracker.Reset(player);
        FluidMovementSystem.Reset();
        HeadAttachSystem.Reset();
        if (player != null && player.ProceduralWeaponAnimation != null)
        {
            player.ProceduralWeaponAnimation.UpdateWeaponVariables();
            ApplyHeadRotation(player);
        }
        HandlingState.Clear();
    }

    private static void ApplyHeadRotation(Player player)
    {
        ProceduralWeaponAnimation proceduralWeaponAnimation = player.ProceduralWeaponAnimation;
        if (proceduralWeaponAnimation != null)
        {
            proceduralWeaponAnimation.SetHeadRotation(player.HeadRotation);
        }
    }

    public static Player LocalPlayer()
    {
        Player player = Singleton<GameWorld>.Instance?.MainPlayer;
        if (!(player != null) || !player.IsYourPlayer)
        {
            return null;
        }
        return player;
    }
}