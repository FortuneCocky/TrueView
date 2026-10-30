using EFT;
using TrueView.Configuration;
using TrueView.Core;
using UnityEngine;

namespace TrueView.Systems;

/// <summary>
/// Procedural hip deadzone. Think of the deadzone cone as a red box on screen:
/// the arms/weapon move freely inside it while the camera stays completely
/// locked. Only when the weapon reaches the edge of the box does the camera
/// start following. A larger cone = more arm movement before the camera moves.
/// Sprinting smoothly blends the effect out and back in for natural transitions.
///
/// Natural motion layers (controlled by HipNaturalMotion):
///  - Organic Perlin drift: subtle micro-tremors simulating hands holding a gun
///  - Mouse whip: weapon lags behind fast mouse flicks then springs back
///  - Directional tilt: weapon rolls/tilts in the direction of mouse motion
/// </summary>
internal static class ProceduralHipSystem
{
    private static Quaternion _weaponRot = Quaternion.identity;
    private static Quaternion _cameraRot = Quaternion.identity;
    private static bool _initialized;
    private static float _cameraSuppressUntil;
    private static bool _lastAiming;
    private static float _zoneTransitionStart;
    private static float _zoneTransitionEnd;
    private static bool _handsTransitionActive;
    private static bool _recoilReturnActive;
    private static Quaternion _recoilAnchor = Quaternion.identity;

    // Sprint blend: 0 = full hip deadzone, 1 = full sprint (no deadzone).
    private static float _sprintBlend;
    private static float _sprintVelocity;

    // --- Natural motion state ---
    // Organic Perlin drift (micro-tremors / breathing while holding at hip)
    private static float _driftTime;
    private static Vector2 _organicOffset;

    // Mouse whip (weapon lags behind fast flicks, springs back)
    private static Vector2 _whipOffset;
    private static Vector2 _whipVelocity;
    private static Vector2 _lastMouseDelta;

    // Directional tilt (weapon rolls in direction of mouse motion)
    private static float _tiltCurrent;
    private static float _tiltVelocity;

    // --- Procedural arm/wrist motion state ---

    // Strafe wrist roll: weapon rolls when strafing left/right
    private static float _strafeRoll;
    private static float _strafeRollVel;

    // Acceleration sway: weapon pitches forward/back from movement inertia
    private static float _accelPitch;
    private static float _accelPitchVel;
    private static float _lastSpeed;

    // Idle wrist roll: subtle random wrist micro-adjustments
    private static float _wristRollTime;
    private static float _wristRollCurrent;
    private static float _wristRollVel;

    // --- Body turn lead state ---
    // Wrist-turn effect: yaw swings the muzzle ahead of the turn, roll banks
    // the gun like a wrist twist. Springs settle it back to neutral.
    private static float _zoneDeg = 12f;   // deadzone limit in deg
    private static float _lagYaw;          // signed yaw of weapon vs camera
    private static float _leadYaw;
    private static float _leadYawVel;
    private static float _leadRoll;
    private static float _leadRollVel;
    private static float _adsCant;
    private static float _adsCantVel;

    // ADS entry lift — decays from 1 to 0, giving the gun a brief rise that
    // settles back into the normal ADS pose.
    private static float _adsLift;
    private static float _adsFloatTime;
    private static Vector2 _adsFloat;

    // --- Lazy weapon physics (spring-mass-damper follow) ---
    // Angular velocity of the weapon tracker (deg/s) when lazy physics drives
    // the camera-follow instead of exponential Slerp.
    private static float _lazyYawVel;
    private static float _lazyPitchVel;

    // Sprung weapon position — the gun's translation chases the target pose
    // through its own under-damped spring so positional layers (momentum dips,
    // gait bob, pivot lead) lag and settle instead of tracking rigidly.
    private static Vector3 _lazyPos;
    private static Vector3 _lazyPosVel;

    // Low-passed spring target + previous error — the jitter guards. Head-cam
    // systems feeding back into the camera rotation would otherwise inject
    // frame-rate noise into the spring and ring.
    private static Quaternion _lazyTarget = Quaternion.identity;
    private static float _lazyPrevErrYaw;
    private static float _lazyPrevErrPitch;

    // Weapon bend on turn — the muzzle lags behind the grip during yaws,
    // flexing the gun about a pivot in the hands.
    private static float _bendYaw;
    private static float _bendYawVel;
    private static float _bendInput;

    // Cached weapon offset — computed once per Tick, read many times per frame
    private static Quaternion _cachedWeaponOffset = Quaternion.identity;
    private static Vector3 _cachedWeaponPosOffset = Vector3.zero;

    /// <summary>Where the camera should look (locked until weapon hits edge).</summary>
    public static Vector3 CameraEuler => _cameraRot.eulerAngles;

    /// <summary>True once the hip system has a valid weapon/camera pose.</summary>
    public static bool HasPose => _initialized;

    public static Vector2 DebugOffset { get; private set; }

    public static float DebugAngle { get; private set; }

    public static Vector2 ControlOffset { get; private set; }

    public static float ControlAngle { get; private set; }

    public static bool ControlsCamera => false;

    public static bool IsZoneTransitionActive => _handsTransitionActive ||
        Time.unscaledTime < _zoneTransitionEnd || Time.unscaledTime < _cameraSuppressUntil;

    public static float EffectiveZoneDegrees(float configuredZone)
    {
        if (_handsTransitionActive)
            return Mathf.Max(45f, configuredZone);
        if (Time.unscaledTime >= _zoneTransitionEnd)
            return configuredZone;

        float duration = Mathf.Max(0.01f, _zoneTransitionEnd - _zoneTransitionStart);
        float progress = Mathf.Clamp01((Time.unscaledTime - _zoneTransitionStart) / duration);
        float eased = progress * progress * (3f - 2f * progress);
        return Mathf.Lerp(Mathf.Max(45f, configuredZone), configuredZone, eased);
    }

    public static float GreenZoneDegrees(float whiteZone) => Mathf.Max(0.5f, whiteZone * 0.55f);

    private static void UpdateControlOffset()
    {
        if (!_initialized)
        {
            ControlOffset = Vector2.zero;
            ControlAngle = 0f;
            return;
        }

        Vector3 relative = (_weaponRot * Quaternion.Inverse(_cameraRot)).eulerAngles;
        ControlOffset = new Vector2(
            Mathf.DeltaAngle(0f, relative.y),
            Mathf.DeltaAngle(0f, relative.x));
        ControlAngle = Quaternion.Angle(_cameraRot, _weaponRot);
    }

    public static void AddImpulse(float yaw, float pitch)
    {
        if (!_initialized)
            return;

        if (!_recoilReturnActive)
        {
            _recoilAnchor = _weaponRot;
            _recoilReturnActive = true;
        }
        _weaponRot = Quaternion.Euler(pitch, yaw, 0f) * _weaponRot;
    }

    /// <summary>Current hip weapon rotation (used to seed the ADS deadzone).</summary>
    public static Quaternion WeaponRotation => _weaponRot;

    /// <summary>Current hip camera rotation (used to seed the ADS deadzone).</summary>
    public static Quaternion CameraRotation => _cameraRot;

    /// <summary>Relative weapon offset applied to WeaponRoot (weapon vs camera).</summary>
    public static Quaternion WeaponOffset => _cachedWeaponOffset;

    /// <summary>Relative weapon positional offset applied to WeaponRoot —
    /// used for pivot compensation (wrist turns) and the ADS entry lift.</summary>
    public static Vector3 WeaponPosOffset => _cachedWeaponPosOffset;

    // Tracks the positional offset we actually wrote into WeaponRoot so the
    // next frame can remove it if the game didn't reset the transform —
    // prevents accumulation on weapons (e.g. pistols) whose position isn't
    // rewritten every frame.
    private static Vector3 _appliedPosOffset = Vector3.zero;
    private static Vector3 _lastPosResult = Vector3.zero;

    /// <summary>
    /// Applies the positional offset to WeaponRoot safely. If the game did
    /// not reset the transform since our last write, the previous offset is
    /// removed first — so the offset can never accumulate frame to frame.
    /// </summary>
    public static void ApplyWeaponPosOffset(Transform weaponRoot, Transform camera = null)
    {
        if (_appliedPosOffset != Vector3.zero && weaponRoot.localPosition == _lastPosResult)
            weaponRoot.localPosition -= _appliedPosOffset;
        Vector3 offset = _cachedWeaponPosOffset;
        // Camera-space head deltas: the camera translates but WeaponRoot is
        // its sibling, so without this the gun stays while the view moves —
        // the weapon spring then chases the gap every frame (gun jitter
        // while the head camera moves vertically/positionally).
        Vector3 head = HeadAttachSystem.LastPosDelta;
        if (camera != null && head.sqrMagnitude > 0f && weaponRoot.parent != null)
            offset += weaponRoot.parent.InverseTransformVector(camera.TransformVector(head));
        weaponRoot.localPosition += offset;
        _appliedPosOffset = offset;
        _lastPosResult = weaponRoot.localPosition;
    }

    public static void Tick(Player player, float deltaTime)
    {
        if (deltaTime <= 0f || player == null)
        {
            return;
        }

        bool isAiming = HandlingState.IsAiming;
        bool modeEnabled = isAiming ? HandlingConfig.DeadzoneEnabled.Value : HandlingConfig.HipDeadzoneEnabled.Value;
        if (!modeEnabled || !HandlingState.HasWeapon)
        {
            Reset();
            return;
        }

        MovementContext mc = player.MovementContext;
        if (mc != null && (mc.IsInMountedState || (mc.CurrentState != null && mc.CurrentState.Name == EPlayerState.Stationary)))
        {
            Reset();
            return;
        }

        if (isAiming != _lastAiming)
        {
            BeginZoneTransition(0.65f);
            _lastAiming = isAiming;
            if (isAiming)
                _adsLift = 1f;
        }

        // Smoothly blend sprint state instead of hard reset.
        float sprintTarget = HandlingState.IsSprinting ? 1f : 0f;
        float dt = Mathf.Min(deltaTime, 0.05f);
        // Tracking Slerps use the real frame time (capped for sanity) —
        // Slerp can't overshoot, and using the clamped spring dt makes the
        // tracker fall behind below 20fps until the catch-up snap ratchets.
        float trackDt = Mathf.Min(deltaTime, 0.2f);
        SpringSprint(ref _sprintBlend, ref _sprintVelocity, sprintTarget, 10f, dt);

        Quaternion target = Quaternion.Euler(player.HeadRotation);
        if (!_initialized)
        {
            _weaponRot = target;
            _cameraRot = target;
            _initialized = true;
        }

        _cameraRot = target;

        WeaponStats stats = HandlingState.Stats;
        float fatigue = 1f - HandlingState.HandsStamina01;
        float movement = HandlingState.MovementSpeed01;
        float adsTightness = isAiming ? HandlingConfig.AdsFollowTightness.Value : 0f;
        float weaponFollowSpeed;
        float weaponFollowT;
        if (isAiming && adsTightness >= 0.999f)
        {
            weaponFollowSpeed = 1000f;
            weaponFollowT = 1f;
        }
        else
        {
            float responsiveness = isAiming ? Mathf.Pow(adsTightness, 1.5f) : HandlingConfig.HipDeadzoneSmoothing.Value;
            weaponFollowSpeed = isAiming ? Mathf.Lerp(2.5f, 24f, responsiveness) : Mathf.Lerp(5f, 15f, responsiveness);
            weaponFollowSpeed *= Mathf.Lerp(1.2f, 0.52f, stats.WeightProgress);
            weaponFollowSpeed *= Mathf.Lerp(0.72f, 1.2f, stats.Ergonomics01);
            if (isAiming)
                weaponFollowSpeed *= Mathf.Lerp(1f, 0.58f, fatigue) * Mathf.Lerp(1f, 0.7f, movement);
            weaponFollowSpeed *= LocomotionGaitSystem.WeaponFollowMultiplier * InjuryHandlingSystem.WeaponFollowMultiplier * TurnInPlaceSystem.WeaponFollowMultiplier;
            weaponFollowSpeed = Mathf.Lerp(weaponFollowSpeed, 24f, _sprintBlend);
            weaponFollowT = 1f - Mathf.Exp(-weaponFollowSpeed * trackDt);
        }

        // Lazy physics is hip-only — ADS keeps the classic direct tracking.
        bool lazyPhysics = HandlingConfig.LazyPhysicsEnabled?.Value == true
            && !isAiming;

        if (_recoilReturnActive)
        {
            _recoilAnchor = Quaternion.Slerp(_recoilAnchor, target, weaponFollowT);
            float velDecay = Mathf.Exp(-8f * trackDt);
            _lazyYawVel *= velDecay;
            _lazyPitchVel *= velDecay;
        }
        else if (lazyPhysics)
        {
            IntegrateLazyRotation(target, weaponFollowSpeed, deltaTime, stats.WeightProgress);
        }
        else
        {
            float velDecay = Mathf.Exp(-10f * trackDt);
            _lazyYawVel *= velDecay;
            _lazyPitchVel *= velDecay;
            _weaponRot = Quaternion.Slerp(_weaponRot, target, weaponFollowT);
        }

        float deadzoneInfluence = (isAiming
            ? (1f - adsTightness) * Mathf.Lerp(0.25f, 1.25f, stats.WeightProgress) *
                Mathf.Lerp(1f, 1.8f, fatigue) * Mathf.Lerp(1f, 1.45f, movement)
            : HandlingConfig.HipDeadzoneInfluence.Value * 3f) * (1f - _sprintBlend * 0.7f);
        if (deadzoneInfluence > 0f)
        {
            float proceduralDt = Mathf.Min(deltaTime, 1f / 30f);
            float frameScale = proceduralDt * 60f;
            float maxDrift = isAiming ? 0.6f : 1.2f;
            float yawDrift = Mathf.Clamp(MotionTracker.YawRate * deadzoneInfluence * 0.01f * frameScale, -maxDrift, maxDrift);
            float pitchDrift = Mathf.Clamp(MotionTracker.PitchRate * deadzoneInfluence * 0.01f * frameScale, -maxDrift, maxDrift);
            _weaponRot = Quaternion.Euler(pitchDrift, yawDrift, 0f) * _weaponRot;
        }

        float baseZone = Mathf.Max(0.1f, EffectiveZoneDegrees(isAiming
            ? HandlingConfig.DeadzoneAds.Value
            : HandlingConfig.HipCameraFollowZone.Value));
        float maxWeaponLag = baseZone;
        // Turn range expansion — the cone widens with turn speed so the gun
        // can sweep far past its resting deadzone mid-turn ("bend the gun around
        // the corner"). Shrinks back as the turn ends.
        float turnExpand = HandlingConfig.TurnConeExpand?.Value ?? 0f;
        if (turnExpand > 0.001f)
        {
            float turnAmount = Mathf.Clamp01(Mathf.Abs(MotionTracker.YawRate) / 280f);
            maxWeaponLag = baseZone * (1f + turnAmount * turnExpand);
        }
        // Hard ceiling — expanded or not, the weapon never leaves the frame.
        // ~22° hip keeps the muzzle visible near screen edge at typical FOVs;
        // 9° in ADS keeps the optic picture readable.
        maxWeaponLag = Mathf.Min(maxWeaponLag, Mathf.Max(baseZone, isAiming ? 9f : 22f));
        if (HandlingConfig.HardDeadzone.Value) maxWeaponLag = baseZone;
        _zoneDeg = baseZone;
        _lagYaw = Mathf.DeltaAngle(0f, (Quaternion.Inverse(target) * _weaponRot).eulerAngles.y);
        float weaponLag = Quaternion.Angle(_weaponRot, target);
        if (weaponLag > maxWeaponLag)
        {
            float excessT = (weaponLag - maxWeaponLag) / weaponLag;
            float restraintT = 1f - Mathf.Exp(-18f * Mathf.Min(deltaTime, 1f / 30f));
            _weaponRot = Quaternion.Slerp(_weaponRot, target, excessT * restraintT);
        }

        if (_recoilReturnActive)
        {
            float returnSpeed = ShotSystem.InShotRecovery ? 3.5f : 11f;
            _weaponRot = Quaternion.Slerp(
                _weaponRot,
                _recoilAnchor,
                1f - Mathf.Exp(-returnSpeed * trackDt));
            if (!ShotSystem.InShotRecovery && Quaternion.Angle(_weaponRot, _recoilAnchor) < 0.05f)
            {
                _weaponRot = _recoilAnchor;
                _recoilReturnActive = false;
            }
        }

        UpdateControlOffset();

        // ADS entry lift decay — exponential so it eases out, not linear.
        _adsLift *= Mathf.Exp(-5.5f * trackDt);
        if (_adsLift < 0.001f)
            _adsLift = 0f;

        // --- Natural motion layers (blended out during sprint) ---
        UpdateNaturalMotion(dt, sprintTarget);
        UpdateProceduralArmMotion(player, dt);
        UpdateBodyTurnLead(dt);

        float automaticAdsFloat = isAiming && !HandlingState.IsHoldingBreath
            ? (1f - adsTightness) * (0.06f + stats.WeightProgress * 0.14f + fatigue * 0.38f + movement * 0.28f)
            : 0f;
        if (automaticAdsFloat > 0.001f)
        {
            _adsFloatTime += dt * Mathf.Lerp(0.65f, 1.15f, fatigue + movement * 0.5f);
            Vector2 floatTarget = new(
                (Mathf.PerlinNoise(_adsFloatTime, 17.3f) - 0.5f) * 2f * automaticAdsFloat,
                (Mathf.PerlinNoise(31.7f, _adsFloatTime) - 0.5f) * 2f * automaticAdsFloat * 0.7f);
            _adsFloat = Vector2.Lerp(_adsFloat, floatTarget, 1f - Mathf.Exp(-4f * dt));
        }
        else
        {
            _adsFloat = Vector2.Lerp(_adsFloat, Vector2.zero, 1f - Mathf.Exp(-8f * dt));
        }

        // --- Cache the weapon offset quaternion (computed once per tick) ---
        CacheWeaponOffset();
        UpdateFireDirection(player);
    }

    /// <summary>
    /// Computes and caches the weapon offset quaternion. Called once per Tick
    /// so the WeaponOffset getter is a cheap field read instead of doing
    /// quaternion math on every access (which can be many times per frame).
    /// </summary>
    private static void CacheWeaponOffset()
    {
        if (!_initialized)
        {
            _cachedWeaponOffset = Quaternion.identity;
            _cachedWeaponPosOffset = Vector3.zero;
            DebugOffset = Vector2.zero;
            DebugAngle = 0f;
            ControlOffset = Vector2.zero;
            ControlAngle = 0f;
            return;
        }

        Quaternion baseOffset = _weaponRot * Quaternion.Inverse(_cameraRot);

        // Strip accumulated roll: chained yaw/pitch quaternion deltas
        // always leak a little roll into _weaponRot, and nothing ever
        // removed it — so after a fast whip the gun stayed tilted until a
        // full Reset. We rewrite _weaponRot to the roll-free equivalent so
        // the error can't keep compounding inside the tracker either.
        // Deliberate roll layers (tilt/lead/strafe) are added below as
        // clean euler terms and are unaffected.
        Vector3 baseEuler = baseOffset.eulerAngles;
        baseOffset = Quaternion.Euler(
            Mathf.DeltaAngle(0f, baseEuler.x),
            Mathf.DeltaAngle(0f, baseEuler.y),
            0f);
        _weaponRot = baseOffset * _cameraRot;
        UpdateControlOffset();
        if (_recoilReturnActive)
        {
            Vector3 anchorEuler = (_recoilAnchor * Quaternion.Inverse(_cameraRot)).eulerAngles;
            _recoilAnchor = Quaternion.Euler(
                Mathf.DeltaAngle(0f, anchorEuler.x),
                Mathf.DeltaAngle(0f, anchorEuler.y),
                0f) * _cameraRot;
        }

        Vector3 posOffset = Vector3.zero;

        bool applyHipMotion = !HandlingState.IsAiming;
        float naturalStrength = HandlingConfig.HipNaturalMotion.Value;
        if (applyHipMotion && naturalStrength > 0.001f)
        {
            Vector2 drift = _organicOffset * naturalStrength;
            Vector2 whip = _whipOffset * naturalStrength;
            float tilt = _tiltCurrent * naturalStrength;

            Vector3 naturalEuler = new Vector3(
                drift.y + whip.y,
                drift.x + whip.x,
                tilt);

            baseOffset = Quaternion.Euler(naturalEuler) * baseOffset;
        }

        // Procedural arm/wrist motion layers (independent of HipNaturalMotion)
        Vector3 armEuler = Vector3.zero;
        // Acceleration sway: pitch from starting/stopping
        armEuler.x += _accelPitch;
        // Strafe wrist roll: roll from lateral movement
        armEuler.z += _strafeRoll;
        // Idle wrist roll: subtle random roll
        armEuler.z += _wristRollCurrent;
        if (applyHipMotion && armEuler != Vector3.zero)
            baseOffset = Quaternion.Euler(armEuler) * baseOffset;

        if (HandlingState.IsAiming && _adsFloat != Vector2.zero)
            baseOffset = Quaternion.Euler(_adsFloat.y, _adsFloat.x, 0f) * baseOffset;

        // Hard deadzone: lag + wrist lead + muzzle bend share one yaw budget.
        // Smooth tanh limit keeps the total inside the zone so the gun never
        // swings off screen, while still using the whole zone on fast turns.
        float leadYawUse = _leadYaw, bendYawUse = _bendYaw;
        if (HandlingConfig.HardDeadzone.Value)
        {
            float extra = _leadYaw + _bendYaw;
            if (Mathf.Abs(extra) > 0.001f)
            {
                float z = Mathf.Max(0.5f, _zoneDeg);
                float total = _lagYaw + extra;
                float limited = z * (float)System.Math.Tanh(total / z);
                float f = Mathf.Clamp01((limited - _lagYaw) / extra);
                if (Mathf.Sign(limited - _lagYaw) != Mathf.Sign(extra)) f = 0f;
                leadYawUse *= f; bendYawUse *= f;
            }
        }

        float leadStrength = applyHipMotion
            ? HandlingConfig.HipBodyTurnLead.Value
            : HandlingConfig.AdsWeaponLead?.Value ?? 0f;
        if (leadStrength > 0.001f)
        {
            Quaternion leadRot = Quaternion.Euler(0f, leadYawUse, _leadRoll);
            // Pivot the turn at the hands: the rotation is applied about a
            // point at the grip (below/ahead of the camera) instead of the
            // weapon root, so the grip stays planted while the muzzle
            // sweeps — reads as the wrist bending, not the gun sliding.
            // ADS: pivot at the sight plane so the dot stays on screen.
            Vector3 pivot = Vector3.Lerp(new Vector3(0f, -0.12f, 0.08f), MuzzleSightPivot, _pivotBlend);
            posOffset += pivot - leadRot * pivot;
            baseOffset = leadRot * baseOffset;
        }

        if (Mathf.Abs(bendYawUse) > 0.001f)
        {
            // Muzzle lead: the gun pivots in the hands. Hip: about the grip,
            // so the butt swings the opposite way while the muzzle sweeps out
            // ahead of the turn, banking and rising a little as it goes.
            // ADS: about the sight plane, so the sights stay on screen.
            float sweep = bendYawUse;
            Quaternion bendRot = Quaternion.Euler(-0.08f * Mathf.Abs(sweep), sweep, -0.3f * sweep);
            Vector3 pivot = Vector3.Lerp(MuzzleHandPivot, MuzzleSightPivot, _pivotBlend);
            posOffset += pivot - bendRot * pivot;
            baseOffset = bendRot * baseOffset;
        }

        if (InjuryHandlingSystem.ArmPenalty > 0.001f)
        {
            Vector3 injuryRotation = new(
                InjuryHandlingSystem.ArmPenalty * 0.45f,
                InjuryHandlingSystem.ArmBias * 0.8f,
                InjuryHandlingSystem.ArmBias * 2.5f);
            baseOffset = Quaternion.Euler(injuryRotation) * baseOffset;
        }

        posOffset += TurnInPlaceSystem.WeaponPosOffset;
        baseOffset = TurnInPlaceSystem.WeaponRotationOffset * baseOffset;

        // ADS entry lift — the gun rises briefly then settles back into the
        // normal ADS pose. Sin-curve-shaped decay keeps it soft at both ends.
        if (HandlingState.IsAiming && _adsLift > 0f)
        {
            float lift = _adsLift * _adsLift; // ease-out shape
            posOffset.y += 0.022f * lift;
            baseOffset = Quaternion.Euler(-4f * lift, 0f, 0f) * baseOffset;
        }

        // Canted aim: the gun rolls about the sight line instead of sitting
        // level, and the sight drifts a little off-centre with the cant.
        if (Mathf.Abs(_adsCant) > 0.01f)
        {
            Quaternion cantRot = Quaternion.Euler(0f, 0f, _adsCant);
            posOffset += MuzzleSightPivot - cantRot * MuzzleSightPivot;
            posOffset += new Vector3(-0.0011f * _adsCant, -0.0007f * Mathf.Abs(_adsCant), 0f);
            baseOffset = cantRot * baseOffset;
        }

        // ADS swing-in arc (and the smaller drop-back arc when leaving the sights).
        if (TurnRollSystem.ArcActive)
        {
            posOffset += TurnRollSystem.ArcPos;
            baseOffset = TurnRollSystem.ArcRot * baseOffset;
        }

        float blend = 1f - _sprintBlend;
        _cachedWeaponOffset = Quaternion.Slerp(Quaternion.identity, baseOffset, blend);

        Vector3 posTarget = posOffset * blend;
        float posLag = HandlingConfig.LazyPosLag?.Value ?? 0f;
        bool posSpring = HandlingConfig.LazyPhysicsEnabled?.Value == true
            && !HandlingState.IsAiming && posLag > 0.001f;
        if (posSpring)
        {
            float sdt = Mathf.Min(Time.deltaTime, 1f / 90f);
            float w = Mathf.Clamp01(HandlingState.Stats.WeightProgress) * HandlingConfig.LazyWeightInfluence.Value;
            float omega = Mathf.Clamp(9f * HandlingConfig.LazyTension.Value * Mathf.Lerp(1f, 0.6f, w), 1.5f, 30f);
            float zeta = Mathf.Clamp(0.85f * Mathf.Lerp(1f, 0.65f, w), 0.2f, 1.2f);
            _lazyPosVel += ((posTarget - _lazyPos) * (omega * omega) - _lazyPosVel * (2f * zeta * omega)) * sdt;
            _lazyPos += _lazyPosVel * sdt;
            _cachedWeaponPosOffset = Vector3.Lerp(posTarget, _lazyPos, posLag);
        }
        else
        {
            _lazyPos = posTarget;
            _lazyPosVel = Vector3.zero;
            _cachedWeaponPosOffset = posTarget;
        }
    }

    /// <summary>
    /// Lazy physics rotation integrator: treats the weapon as a sprung mass
    /// chasing the camera. Under-damped below ζ=1 so fast camera motion
    /// overshoots and settles instead of easing monotonically. Stiffness
    /// reuses the existing follow-speed chain so weight, ergonomics, gait,
    /// injury and fatigue scaling all still apply.
    /// </summary>
    private static void IntegrateLazyRotation(Quaternion target, float speed, float deltaTime, float weightProgress)
    {
        float sdt = Mathf.Min(deltaTime, 1f / 90f);
        float w = Mathf.Clamp01(weightProgress) * HandlingConfig.LazyWeightInfluence.Value;
        float omega = Mathf.Clamp(speed * HandlingConfig.LazyTension.Value * Mathf.Lerp(1f, 0.68f, w), 1.5f, 30f);
        float zeta = Mathf.Clamp((1f - HandlingConfig.LazySpringiness.Value * 0.35f) * Mathf.Lerp(1f, 0.72f, w), 0.16f, 1.2f);

        // Low-pass the spring's target: head-cam systems (HeadAttach, camera
        // lag, momentum) write into the camera rotation every frame, and at
        // cranked settings that output carries frame-rate noise. Feeding the
        // raw target into an under-damped spring makes it ring — so the spring
        // chases a smoothed target at ~30Hz-equivalent cutoff. Adds ~8ms of
        // lag, kills the jitter path entirely.
        _lazyTarget = Quaternion.Slerp(_lazyTarget, target, 1f - Mathf.Exp(-30f * sdt));

        Vector3 rel = (_lazyTarget * Quaternion.Inverse(_weaponRot)).eulerAngles;
        float errYaw = Mathf.DeltaAngle(0f, rel.y);
        float errPitch = Mathf.DeltaAngle(0f, rel.x);

        // Deadband — an under-damped spring never truly rests, so at idle the
        // gun micro-twitches forever. Below the perceptible threshold, kill
        // the residual velocity and track directly.
        if (Mathf.Abs(errYaw) < 0.015f && Mathf.Abs(errPitch) < 0.015f &&
            Mathf.Abs(_lazyYawVel) < 1f && Mathf.Abs(_lazyPitchVel) < 1f)
        {
            _lazyYawVel = 0f;
            _lazyPitchVel = 0f;
            _weaponRot = Quaternion.Slerp(_weaponRot, target, 1f - Mathf.Exp(-speed * sdt));
            return;
        }

        float k = omega * omega;
        float c = 2f * zeta * omega;

        // Overshoot-crossing damping: when the error flips sign while the
        // velocity is still carrying, the mass just shot past the target —
        // that's where the jitter cycle starts. Briefly over-damp that axis
        // so it settles instead of ringing.
        if (errYaw * _lazyPrevErrYaw < 0f && Mathf.Abs(_lazyYawVel) > 2f)
            _lazyYawVel *= 0.35f;
        if (errPitch * _lazyPrevErrPitch < 0f && Mathf.Abs(_lazyPitchVel) > 2f)
            _lazyPitchVel *= 0.35f;
        _lazyPrevErrYaw = errYaw;
        _lazyPrevErrPitch = errPitch;

        _lazyYawVel += (errYaw * k - _lazyYawVel * c) * sdt;
        _lazyPitchVel += (errPitch * k - _lazyPitchVel * c) * sdt;
        _lazyYawVel = Mathf.Clamp(_lazyYawVel, -900f, 900f);
        _lazyPitchVel = Mathf.Clamp(_lazyPitchVel, -900f, 900f);
        _weaponRot = Quaternion.Euler(_lazyPitchVel * sdt, _lazyYawVel * sdt, 0f) * _weaponRot;
    }

    private static void UpdateFireDirection(Player player)
    {
        if (player?.HandsController is not Player.FirearmController firearmController || Camera.main == null)
        {
            DebugOffset = Vector2.zero;
            DebugAngle = 0f;
            return;
        }

        try
        {
            Vector3 position = firearmController.CurrentFireport.position;
            Vector3 direction = firearmController.WeaponDirection;
            firearmController.AdjustShotVectors(ref position, ref direction);
            direction.Normalize();

            Camera camera = Camera.main;
            Vector3 localDirection = camera.transform.InverseTransformDirection(direction);
            if (localDirection.z <= 0f)
            {
                DebugOffset = Vector2.zero;
                DebugAngle = 0f;
                return;
            }

            DebugOffset = new Vector2(
                Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg,
                -Mathf.Atan2(localDirection.y, new Vector2(localDirection.x, localDirection.z).magnitude) * Mathf.Rad2Deg);
            DebugAngle = Vector3.Angle(Vector3.forward, localDirection);
        }
        catch
        {
            DebugOffset = Vector2.zero;
            DebugAngle = 0f;
        }
    }

    /// <summary>
    /// Body turn lead — the wrist-turn effect. When the mouse moves, the
    /// muzzle swings out ahead of the turn direction and the gun banks like
    /// the wrist is twisting; when input stops, it springs back to neutral
    /// with a slight overshoot. Heavier weapons swing slower and wider.
    /// </summary>
    private static void UpdateBodyTurnLead(float dt)
    {
        bool aiming = HandlingState.IsAiming;
        _pivotBlend = Mathf.MoveTowards(_pivotBlend, aiming ? 1f : 0f, dt * 6f);
        UpdateAdsCant(dt, aiming);

        float strength = aiming
            ? HandlingConfig.AdsWeaponLead?.Value ?? 0f
            : HandlingConfig.HipBodyTurnLead.Value;
        float bendAvailable = HandlingConfig.HipTurnBend?.Value ?? 0f;
        if (strength <= 0.001f && bendAvailable <= 0.001f)
        {
            _leadYaw = 0f;
            _leadYawVel = 0f;
            _leadRoll = 0f;
            _leadRollVel = 0f;
            _bendYaw = 0f;
            _bendYawVel = 0f;
            _bendInput = 0f;
            return;
        }

        WeaponStats stats = HandlingState.Stats;
        float sprintFade = 1f - _sprintBlend * 0.85f;
        float weightFade = 1f - stats.WeightProgress * 0.25f;
        float strengthT = Mathf.Clamp01(strength / 3f);

        // Large swing — this must read as the wrist physically rotating the
        // gun, not a small offset. 10° subtle to 30° dramatic.
        float normalizedTurn = Mathf.Clamp(MotionTracker.YawRate / 280f, -1f, 1f);
        // ADS: smaller swing so the sight stays usable, but a heavier bank —
        // the roll is what sells the twist when the gun is on the eye.
        float yawTarget = normalizedTurn * (aiming ? Mathf.Lerp(4f, 14f, strengthT) : Mathf.Lerp(10f, 30f, strengthT))
            * sprintFade * weightFade;

        // Banking into the turn is what reads as "the wrist turned" —
        // without roll it just looks like the gun slid sideways.
        float rollTarget = -yawTarget * (aiming ? Mathf.Lerp(0.9f, 1.4f, strengthT) : Mathf.Lerp(0.45f, 0.75f, strengthT));

        // Slightly underdamped spring: fast attack, settles back with a
        // small overshoot. Heavier weapons get a lazier swing.
        float freq = Mathf.Lerp(6f, 3.5f, stats.WeightProgress);
        SpringFloat(ref _leadYaw, ref _leadYawVel, yawTarget, freq, dt);
        SpringFloat(ref _leadRoll, ref _leadRollVel, rollTarget, freq, dt);

        UpdateMuzzleLead(dt, stats, sprintFade);
    }

    /// <summary>
    /// Canted aim. While aiming the gun rests at a cant instead of level, then
    /// rolls further into turns and strafes and wanders slowly with the hands,
    /// so the sight picture is never perfectly squared up. Springs back to
    /// level when leaving the sights.
    /// </summary>
    private static void UpdateAdsCant(float dt, bool aiming)
    {
        float target = 0f;
        if (aiming)
        {
            float dyn = HandlingConfig.AdsDynamicCant?.Value ?? 0f;
            float turn = (float)System.Math.Tanh(MotionTracker.YawRate / 200f) * 7f;
            float strafe = MotionTracker.InputDirection.x * MotionTracker.NormalizedSpeed * 5f;
            // hand wander: faint when standing still so a still sight picture stays level
            float wander = (Mathf.PerlinNoise(Time.time * 0.35f, 7.3f) - 0.5f) * 8f
                * Mathf.Lerp(0.2f, 1f, HandlingState.MovementSpeed01);
            target = -(turn + strafe - wander) * dyn;
            target = Mathf.Clamp(target, -40f, 40f);
        }
        float freq = Mathf.Lerp(5f, 3.5f, HandlingState.Stats.WeightProgress);
        SpringFloat(ref _adsCant, ref _adsCantVel, target, freq, dt);
    }

    // Grip (hip) and sight-plane (ADS) pivots, in weapon-root space.
    private static readonly Vector3 MuzzleHandPivot = new Vector3(0f, -0.14f, 0.22f);
    private static readonly Vector3 MuzzleSightPivot = new Vector3(0f, 0f, -0.04f);
    private static float _pivotBlend;
    private static float _bendPrevRate;

    /// <summary>
    /// Muzzle lead. The muzzle is a sprung angle that swings out ahead of the
    /// turn, further than the turn itself, then returns through centre with a
    /// visible overshoot (underdamped). Two inputs drive it: a saturating
    /// response to turn speed (so ordinary turns already lead), and an impulse
    /// on turn acceleration (a flick starts the throw before the speed builds).
    /// Heavier guns swing slower and wider.
    /// </summary>
    private static void UpdateMuzzleLead(float dt, WeaponStats stats, float sprintFade)
    {
        float strength = HandlingConfig.HipTurnBend?.Value ?? 0f;
        if (strength <= 0.001f)
        {
            _bendYaw = 0f;
            _bendYawVel = 0f;
            _bendPrevRate = 0f;
            return;
        }
        bool aiming = HandlingState.IsAiming;

        float rate = MotionTracker.YawRate;                        // deg/s, + = turning right
        float amp = 18f * Mathf.Pow(strength, 0.85f);               // deg at full-speed turns
        amp *= sprintFade * Mathf.Lerp(1f, 1.5f, stats.WeightProgress);
        if (aiming) amp *= HandlingConfig.AdsTurnBend?.Value ?? 0.6f;
        float target = Mathf.Clamp((float)System.Math.Tanh(rate / 160f) * amp, -55f, 55f);

        // flick impulse: change in turn speed throws the muzzle ahead
        float dRate = Mathf.Clamp(rate - _bendPrevRate, -400f, 400f);
        _bendPrevRate = rate;
        _bendYawVel += dRate * 0.3f * Mathf.Min(strength, 3f) * (aiming ? 0.4f : 1f) * sprintFade;

        // underdamped spring (zeta 0.5): overshoots a little past centre
        float wn = Mathf.Lerp(15f, 10f, stats.WeightProgress);
        float zeta = 0.5f;
        float h = dt * 0.5f;
        for (int i = 0; i < 2; i++)
        {
            _bendYawVel += ((target - _bendYaw) * wn * wn - _bendYawVel * 2f * zeta * wn) * h;
            _bendYaw += _bendYawVel * h;
        }
        _bendYaw = Mathf.Clamp(_bendYaw, -60f, 60f);
    }

    /// <summary>
    /// Updates all natural motion layers: organic drift, mouse whip, and
    /// directional tilt. All are blended out during sprint.
    /// </summary>
    private static void UpdateNaturalMotion(float dt, float sprintBlend)
    {
        float naturalStrength = HandlingConfig.HipNaturalMotion.Value;
        if (naturalStrength <= 0.001f)
        {
            _organicOffset = Vector2.zero;
            _whipOffset = Vector2.zero;
            _whipVelocity = Vector2.zero;
            _tiltCurrent = 0f;
            _tiltVelocity = 0f;
            return;
        }

        float sprintFade = 1f - _sprintBlend * 0.85f;

        // --- Organic Perlin drift ---
        // Simulates micro-tremors and breathing while holding a weapon at hip.
        // Uses two Perlin noise channels at different frequencies for yaw/pitch.
        float driftStrength = HandlingConfig.HipOrganicDrift.Value * sprintFade;
        if (driftStrength > 0.001f)
        {
            _driftTime += dt * 0.8f;
            // Weapon weight makes the drift slower and wider
            WeaponStats stats = HandlingState.Stats;
            float weightScale = 1f + 0.3f * stats.WeightProgress;
            float driftAmp = 0.4f * driftStrength * weightScale;
            // Two octaves for more organic feel
            float yawNoise = (Mathf.PerlinNoise(_driftTime * 0.7f, 1.3f) - 0.5f) * 2f;
            float pitchNoise = (Mathf.PerlinNoise(2.1f, _driftTime * 0.7f) - 0.5f) * 2f;
            // Add a slower secondary layer
            yawNoise += (Mathf.PerlinNoise(_driftTime * 0.25f, 5.7f) - 0.5f) * 0.5f;
            pitchNoise += (Mathf.PerlinNoise(8.3f, _driftTime * 0.25f) - 0.5f) * 0.5f;
            _organicOffset = new Vector2(yawNoise * driftAmp, pitchNoise * driftAmp * 0.7f);
        }
        else
        {
            _organicOffset = Vector2.zero;
        }

        // --- Mouse whip ---
        // When the player flicks the mouse, the weapon lags behind then springs
        // back. This creates a natural whip effect. The whip is driven by the
        // raw mouse delta (yaw/pitch rate), not the smoothed weapon rotation.
        float whipStrength = HandlingConfig.HipMouseWhip.Value * sprintFade;
        if (whipStrength > 0.001f)
        {
            // Raw mouse delta this frame (degrees)
            float yawDelta = MotionTracker.YawRate * dt;
            float pitchDelta = MotionTracker.PitchRate * dt;
            Vector2 mouseDelta = new Vector2(yawDelta, pitchDelta);

            // The whip target is opposite to the mouse motion (weapon lags behind)
            // Scaled by whip strength and weapon weight (heavier = more lag)
            WeaponStats stats = HandlingState.Stats;
            float weightLag = 1f + 0.4f * stats.WeightProgress;
            Vector2 whipTarget = -mouseDelta * whipStrength * 0.3f * weightLag;

            // Clamp the whip so it doesn't go crazy on huge flicks
            whipTarget.x = Mathf.Clamp(whipTarget.x, -3f, 3f);
            whipTarget.y = Mathf.Clamp(whipTarget.y, -2f, 2f);

            // Spring the whip offset toward the target, then it settles back to 0
            // Use a critically-damped spring for natural feel
            float whipFreq = Mathf.Lerp(6f, 3f, stats.WeightProgress); // heavier = slower spring
            SpringVector2(ref _whipOffset, ref _whipVelocity, whipTarget, whipFreq, dt);

            // Decay the whip target back to zero when mouse stops
            // (the spring naturally pulls _whipOffset back to the target, and
            //  when mouse stops, whipTarget → 0, so the weapon settles back)
            _lastMouseDelta = mouseDelta;
        }
        else
        {
            _whipOffset = Vector2.zero;
            _whipVelocity = Vector2.zero;
        }

        // --- Directional tilt ---
        // The weapon rolls/tilts in the direction of mouse yaw motion, like
        // a person's wrists flexing when swinging the gun sideways.
        float tiltStrength = HandlingConfig.HipDirectionalTilt.Value * sprintFade;
        if (tiltStrength > 0.001f)
        {
            // Use the slow yaw rate for smoother tilt
            float yawRate = MotionTracker.YawRate;
            // Target tilt is proportional to yaw rate (clamped)
            float tiltTarget = Mathf.Clamp(yawRate / 180f, -1f, 1f) * tiltStrength * 2.5f;
            // Weapon weight reduces tilt (heavier guns are harder to tilt)
            WeaponStats stats = HandlingState.Stats;
            tiltTarget *= (1f - 0.2f * stats.WeightProgress);
            // Spring the tilt for smooth transitions
            SpringFloat(ref _tiltCurrent, ref _tiltVelocity, tiltTarget, 5f, dt);
        }
        else
        {
            _tiltCurrent = 0f;
            _tiltVelocity = 0f;
        }
    }

    /// <summary>
    /// Updates procedural arm/wrist motion layers: walk sway, strafe roll,
    /// acceleration sway, and idle wrist roll. These simulate realistic
    /// weapon rotation from body movement and grip micro-adjustments.
    /// </summary>
    private static void UpdateProceduralArmMotion(Player player, float dt)
    {
        float sprintFade = 1f - _sprintBlend * 0.85f;
        WeaponStats stats = HandlingState.Stats;
        // Heavier weapons sway more, roll less (harder to rotate wrists)
        float weightSway = 1f + 0.3f * stats.WeightProgress;
        float weightRoll = 1f - 0.25f * stats.WeightProgress;

        // --- Strafe wrist roll ---
        // When strafing left/right, the weapon rolls in the direction of
        // body momentum, like wrists rotating with the swing.
        float strafeStrength = HandlingConfig.HipStrafeRoll.Value * sprintFade;
        if (strafeStrength > 0.001f)
        {
            // Use lateral input direction for strafe detection
            float strafeInput = MotionTracker.InputDirection.x;
            // Also factor in actual lateral velocity for smoothness
            float yaw = player.Rotation.x * Mathf.Deg2Rad;
            float sin = Mathf.Sin(yaw);
            float cos = Mathf.Cos(yaw);
            float localRight = player.Velocity.x * cos - player.Velocity.z * sin;
            float lateralVel = Mathf.Clamp(localRight * 0.05f, -1.5f, 1.5f);
            // Combine input and velocity for a natural target
            float rollTarget = (strafeInput * 0.6f + lateralVel * 0.4f) * strafeStrength * 2.5f * weightRoll;
            rollTarget = Mathf.Clamp(rollTarget, -8f, 8f);
            SpringFloat(ref _strafeRoll, ref _strafeRollVel, rollTarget, 4f, dt);
        }
        else
        {
            _strafeRoll = Mathf.Lerp(_strafeRoll, 0f, dt * 8f);
            _strafeRollVel = 0f;
        }

        // --- Acceleration sway ---
        // When starting/stopping, the weapon pitches forward/back from arm inertia.
        float accelStrength = HandlingConfig.HipAccelSway.Value * sprintFade;
        if (accelStrength > 0.001f)
        {
            float currentSpeed = MotionTracker.NormalizedSpeed;
            float speedDelta = currentSpeed - _lastSpeed;
            _lastSpeed = currentSpeed;
            // Positive delta = accelerating = weapon lags back (positive pitch)
            // Negative delta = decelerating = weapon tips forward (negative pitch)
            float pitchTarget = Mathf.Clamp(speedDelta * 15f, -5f, 5f) * accelStrength * weightSway;
            // Spring it so it bounces back naturally
            SpringFloat(ref _accelPitch, ref _accelPitchVel, pitchTarget, 3f, dt);
        }
        else
        {
            _accelPitch = Mathf.Lerp(_accelPitch, 0f, dt * 8f);
            _accelPitchVel = 0f;
            _lastSpeed = MotionTracker.NormalizedSpeed;
        }

        // --- Idle wrist roll ---
        // Subtle random wrist micro-adjustments using Perlin noise.
        // Simulates the grip shifting slightly while holding the weapon.
        float wristStrength = HandlingConfig.HipWristRoll.Value * sprintFade;
        if (wristStrength > 0.001f && MotionTracker.NormalizedSpeed < 0.15f)
        {
            _wristRollTime += dt * 0.5f;
            // Slow Perlin noise for organic micro-roll
            float noise = (Mathf.PerlinNoise(_wristRollTime, 11.3f) - 0.5f) * 2f;
            float rollTarget = noise * wristStrength * 0.8f * weightRoll;
            SpringFloat(ref _wristRollCurrent, ref _wristRollVel, rollTarget, 2.5f, dt);
        }
        else
        {
            _wristRollCurrent = Mathf.Lerp(_wristRollCurrent, 0f, dt * 5f);
            _wristRollVel = 0f;
        }
    }

    /// <summary>Returns the player-driven camera rotation unchanged.</summary>
    public static Vector3 Modify(Vector3 headRotation)
    {
        if (!_initialized)
            return headRotation;

        Quaternion rawCamera = Quaternion.Euler(headRotation);
        _cameraRot = rawCamera;
        if (Time.unscaledTime < _cameraSuppressUntil)
        {
            _weaponRot = rawCamera;
            _recoilAnchor = rawCamera;
            _recoilReturnActive = false;
            _lazyYawVel = 0f;
            _lazyPitchVel = 0f;
            _cachedWeaponOffset = Quaternion.identity;
            _cachedWeaponPosOffset = Vector3.zero;
            ControlOffset = Vector2.zero;
            ControlAngle = 0f;
        }
        return headRotation;
    }

    public static void SetHandsTransition(bool active)
    {
        if (active)
        {
            _handsTransitionActive = true;
            _cameraSuppressUntil = Time.unscaledTime + 0.1f;
        }
        else if (_handsTransitionActive)
        {
            _handsTransitionActive = false;
            BeginZoneTransition(0.45f);
        }
    }

    public static void BeginZoneTransition(float seconds)
    {
        _zoneTransitionStart = Time.unscaledTime;
        _zoneTransitionEnd = _zoneTransitionStart + Mathf.Max(0f, seconds);
    }

    public static void SuppressCameraFollow(float seconds)
    {
        _cameraSuppressUntil = Time.unscaledTime + Mathf.Max(0f, seconds);
        BeginZoneTransition(seconds);
    }

    public static void Reset()
    {
        _weaponRot = Quaternion.identity;
        _cameraRot = Quaternion.identity;
        _recoilAnchor = Quaternion.identity;
        _recoilReturnActive = false;
        _lastAiming = false;
        _initialized = false;
        _sprintBlend = 0f;
        _sprintVelocity = 0f;
        _driftTime = 0f;
        _organicOffset = Vector2.zero;
        _whipOffset = Vector2.zero;
        _whipVelocity = Vector2.zero;
        _lastMouseDelta = Vector2.zero;
        _tiltCurrent = 0f;
        _tiltVelocity = 0f;
        _strafeRoll = 0f;
        _strafeRollVel = 0f;
        _accelPitch = 0f;
        _accelPitchVel = 0f;
        _lastSpeed = 0f;
        _wristRollTime = 0f;
        _wristRollCurrent = 0f;
        _wristRollVel = 0f;
        _leadYaw = 0f;
        _leadYawVel = 0f;
        _leadRoll = 0f;
        _leadRollVel = 0f;
        _bendYaw = 0f;
        _bendYawVel = 0f;
        _bendInput = 0f;
        _bendPrevRate = 0f;
        _adsCant = 0f;
        _adsCantVel = 0f;
        _pivotBlend = 0f;
        _adsLift = 0f;
        _adsFloatTime = 0f;
        _adsFloat = Vector2.zero;
        _lazyYawVel = 0f;
        _lazyPitchVel = 0f;
        _lazyTarget = Quaternion.identity;
        _lazyPrevErrYaw = 0f;
        _lazyPrevErrPitch = 0f;
        _lazyPos = Vector3.zero;
        _lazyPosVel = Vector3.zero;
        _lazyPitchVel = 0f;
        _cachedWeaponOffset = Quaternion.identity;
        _cachedWeaponPosOffset = Vector3.zero;
        _appliedPosOffset = Vector3.zero;
        _lastPosResult = Vector3.zero;
        DebugOffset = Vector2.zero;
        DebugAngle = 0f;
        ControlOffset = Vector2.zero;
        ControlAngle = 0f;
    }

    private static void SpringSprint(ref float current, ref float velocity, float target, float speed, float dt)
    {
        float freq = Mathf.Max(0.5f, speed);
        float k = freq * freq;
        float d = 1.4f * freq;
        velocity += ((target - current) * k - velocity * d) * dt;
        current += velocity * dt;
        current = Mathf.Clamp01(current);
    }

    private static void SpringVector2(ref Vector2 current, ref Vector2 velocity, Vector2 target, float frequency, float dt)
    {
        float k = frequency * frequency;
        float d = 1.4f * frequency;
        velocity += ((target - current) * k - velocity * d) * dt;
        current += velocity * dt;
    }

    private static void SpringFloat(ref float current, ref float velocity, float target, float frequency, float dt)
    {
        float k = frequency * frequency;
        float d = 1.4f * frequency;
        velocity += ((target - current) * k - velocity * d) * dt;
        current += velocity * dt;
    }
}
