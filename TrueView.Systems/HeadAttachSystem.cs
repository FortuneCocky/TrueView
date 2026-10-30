using EFT;
using TrueView.Configuration;
using TrueView.Core;
using UnityEngine;

namespace TrueView.Systems;

/// <summary>
/// Head-mounted camera. Samples the character's head bone and applies its
/// short-term motion (steps, lean, body sway) to the camera so the view moves
/// with the body. A low-pass baseline removes slow drift. Vertical movement and
/// yaw are removed so aim direction and up/down motion are untouched. Fades out completely while aiming down sights or leaning.
/// </summary>
internal static class HeadAttachSystem
{
    private const float MaxPositionOffset = 0.12f;
    private const float MaxRotationDegrees = 6f;
    private const float MaxRollDegrees = 24f;

    private static float _blend = 1f;
    private static bool _initialized;

    // Offset applied last frame, so it is never stacked on top of itself if the
    // game doesn't rewrite the camera position every frame.
    private static Vector3 _lastAppliedDelta;
    private static Vector3 _lastResultPos;
    private static Vector3 _shotShake;
    private static Vector3 _shotShakeVelocity;
    private static Vector3 _posKick;
    private static Vector3 _posKickVelocity;
    private static Vector3 _directionLag;
    private static Vector3 _weaponCoupling;
    private static Vector3 _injuryDrift;
    private static float _injuryTime;
    private static int _lastShakeFrame = -1;
    private static int _lastPosKickFrame = -1;

    public static void AddShotImpulse(float yaw, float pitch)
    {
        if (HandlingConfig.HeadAttachEnabled?.Value != true ||
            HandlingConfig.HeadShotShake == null || HandlingConfig.HeadShotShake.Value <= 0f)
            return;

        float amount = HandlingConfig.HeadShotShake.Value;
        Vector3 impulse = new(-pitch * 0.9f, yaw * 0.55f, -yaw * 0.35f);
        _shotShake += impulse * amount;
        _shotShake = Vector3.ClampMagnitude(_shotShake, 4f * amount);
        _shotShakeVelocity += impulse * (HandlingConfig.HeadShotShakeRecovery.Value * 0.7f * amount);
        _shotShakeVelocity = Vector3.ClampMagnitude(_shotShakeVelocity, 30f * amount);

        // Muzzle jolt: a small translational shove straight back — the head
        // gets pushed rearward, not just rotated. Camera-local -Z is backward.
        if (HandlingConfig.MuzzleJoltEnabled?.Value == true)
        {
            float jolt = HandlingConfig.MuzzleJoltAmount?.Value ?? 1f;
            float kick = 0.0035f * jolt * amount;
            _posKickVelocity += new Vector3(-pitch * 0.0004f * jolt, 0f, -kick * 9f);
            _posKickVelocity = Vector3.ClampMagnitude(_posKickVelocity, 0.09f);
        }
    }

    /// <summary>
    /// Direct euler impulse into the shot-shake spring — used by hit reactions
    /// where the offset direction is already computed.
    /// </summary>
    public static void AddHitImpulse(Vector3 euler)
    {
        if (HandlingConfig.HeadAttachEnabled?.Value != true)
            return;

        _shotShake += euler;
        _shotShake = Vector3.ClampMagnitude(_shotShake, 6f);
        _shotShakeVelocity += euler * 7f;
        _shotShakeVelocity = Vector3.ClampMagnitude(_shotShakeVelocity, 35f);
    }

    /// <summary>
    /// The local player was hit: the head is knocked the way the round travelled (pitched back
    /// from the front, turned and tilted from the side), harder for the head, a stomach hit
    /// doubles him forward, a leg hit dips the view. Scaled by damage and "Hit reaction strength".
    /// </summary>
    public static void OnHitTaken(EBodyPart part, Vector3 direction, float damage, Player player)
    {
        if (HandlingConfig.HitReactionEnabled?.Value != true)
            return;
        float s = (HandlingConfig.HitReactionStrength?.Value ?? 0.8f) * Mathf.Clamp(0.35f + damage / 40f, 0.35f, 1.5f);
        if (s <= 0f)
            return;
        Transform view = Camera.main != null ? Camera.main.transform : player.Transform.Original;
        Vector3 local = direction.sqrMagnitude > 1e-4f ? view.InverseTransformDirection(direction.normalized) : Vector3.forward;
        Vector3 euler = new(local.z * 2.2f, local.x * 1.6f, -local.x * 2.4f); // x>0 looks down: hit from the front pitches him back
        float push = 1f;
        switch (part)
        {
            case EBodyPart.Head: euler *= 1.7f; push = 1.5f; break;
            case EBodyPart.Stomach: euler.x = Mathf.Abs(euler.x) * 0.6f + 1.4f; break;
            case EBodyPart.LeftArm: case EBodyPart.RightArm: euler = new Vector3(euler.x * 0.3f, euler.y * 0.8f, euler.z * 0.8f); push = 0.5f; break;
            case EBodyPart.LeftLeg: case EBodyPart.RightLeg: euler = new Vector3(1.6f, euler.y * 0.3f, euler.z * 0.6f); push = 0.6f; break;
        }
        AddHitImpulse(euler * s);
        _posKickVelocity += (local * 0.03f + (part is EBodyPart.LeftLeg or EBodyPart.RightLeg ? Vector3.down * 0.03f : Vector3.zero)) * push * s;
        _posKickVelocity = Vector3.ClampMagnitude(_posKickVelocity, 0.09f);
    }

    public static Vector3 ApplyShotShake(Vector3 headRotation)
    {
        if (HandlingConfig.HeadAttachEnabled?.Value != true)
            return headRotation;

        if (_lastShakeFrame != Time.frameCount)
        {
            _lastShakeFrame = Time.frameCount;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            float recovery = HandlingConfig.HeadShotShakeRecovery.Value;
            float shakeAmount = _shotShake.magnitude;
            float frequency = Mathf.Lerp(recovery * 0.38f, recovery, Mathf.Clamp01(shakeAmount / 1.5f));
            float k = frequency * frequency;
            float d = 2f * frequency;
            _shotShakeVelocity += (-_shotShake * k - _shotShakeVelocity * d) * dt;
            _shotShake += _shotShakeVelocity * dt;
            if (_shotShake.sqrMagnitude < 0.000001f && _shotShakeVelocity.sqrMagnitude < 0.000001f)
            {
                _shotShake = Vector3.zero;
                _shotShakeVelocity = Vector3.zero;
            }

            float yawNormalized = Mathf.Clamp(MotionTracker.YawRate / 360f, -1f, 1f);
            Vector3 directionTarget = new(0f, -yawNormalized * 1.5f, yawNormalized * 0.8f);
            directionTarget *= HandlingConfig.HeadDirectionLag.Value;
            _directionLag = Vector3.Lerp(_directionLag, directionTarget, 1f - Mathf.Exp(-6f * dt));

            float couplingScale = HandlingState.IsAiming ? 0.08f : 0.02f;
            Vector2 weaponOffset = ProceduralHipSystem.ControlOffset;
            Vector3 couplingTarget = new(weaponOffset.y, weaponOffset.x, 0f);
            couplingTarget *= HandlingConfig.HeadWeaponCoupling.Value * couplingScale;
            _weaponCoupling = Vector3.Lerp(_weaponCoupling, couplingTarget, 1f - Mathf.Exp(-9f * dt));

            float injury = InjuryHandlingSystem.HeadPenalty + InjuryHandlingSystem.TorsoPenalty * 0.35f;
            _injuryTime += dt * Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(injury));
            Vector3 injuryTarget = new(
                (Mathf.PerlinNoise(_injuryTime, 41.3f) - 0.5f) * injury * 0.9f,
                (Mathf.PerlinNoise(73.1f, _injuryTime) - 0.5f) * injury * 0.7f,
                (Mathf.PerlinNoise(_injuryTime * 0.7f, 19.9f) - 0.5f) * injury * 1.1f);
            _injuryDrift = Vector3.Lerp(_injuryDrift, injuryTarget, 1f - Mathf.Exp(-3f * dt));
        }

        return headRotation + _shotShake + _directionLag + _weaponCoupling + _injuryDrift;
    }

    // --- Synthesized body-camera state ---
    private static float _stridePhase;
    private static int _stepSide;
    private static Vector3 _stepOffset;
    private static Vector3 _stepVel;
    private static Vector3 _stepEuler;
    private static Vector3 _stepEulerVel;
    private static float _exertion;
    private static Vector3 _adsSettle;
    private static bool _wasAiming;
    private static int _lastApplyFrame = -1;

    /// <summary>Deltas applied to the camera this frame — the weapon patch
    /// reads these to keep the gun coupled to head motion instead of
    /// lagging it (the source of visible gun-vs-view jitter).</summary>
    internal static Vector3 LastPosDelta;
    internal static Vector3 LastEulerDelta;
    private static Vector3 _posDeltaSm;
    private static Vector3 _eulerSm;

    public static void Apply(Player player, Transform camera)
    {
        if (!HandlingConfig.HeadAttachEnabled.Value || player == null || camera == null || camera.parent == null)
        {
            _initialized = false;
            return;
        }
        // One write per frame — if anything calls Apply twice in a frame the
        // delta would stack and read as micro-jitter.
        if (_lastApplyFrame == Time.frameCount)
        {
            return;
        }

        Transform root = player.Transform?.Original;
        if (root == null)
        {
            _initialized = false;
            return;
        }

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

        if (!_initialized)
        {
            _lastAppliedDelta = Vector3.zero;
            _lastResultPos = camera.localPosition;
            _wasAiming = HandlingState.IsAiming;
            _initialized = true;
            return;
        }

        // --- Exertion: body stamina drives breathing depth/rate and step weight ---
        float exertionTarget = 1f - HandlingState.Stamina01;
        // Fast rise while working, slow recovery after sprint (~20s settle).
        float exertionRate = exertionTarget > _exertion ? 1.2f : 0.06f;
        _exertion = Mathf.Lerp(_exertion, exertionTarget, 1f - Mathf.Exp(-exertionRate * 8f * dt));
        float exertionGain = Mathf.Clamp01(_exertion * (HandlingConfig.HeadExertion?.Value ?? 1f));

        // --- Footstep jolts: stride phase advances with horizontal speed ---
        Vector3 vel = player.Velocity;
        float speed = new Vector2(vel.x, vel.z).magnitude;
        float speedNorm = Mathf.Clamp01(speed / 4.5f);
        float stepGain = (HandlingConfig.HeadStepImpact?.Value ?? 0f);
        float aimScale = HandlingState.IsAiming ? 0.45f : 1f;
        _blend = Mathf.MoveTowards(_blend, speed > 0.2f && stepGain > 0.001f ? 1f : 0f, 6f * dt);
        _stridePhase += dt * Mathf.Lerp(1.7f, 3.4f, speedNorm) * _blend;
        int stepIndex = (int)_stridePhase;
        if (stepIndex != _stepSide && _blend > 0.05f)
        {
            _stepSide = stepIndex;
            // Left/right alternating strike: pitch dip + lateral roll, heavier when tired.
            float w = Mathf.Lerp(0.7f, 1.3f, exertionGain);
            float jolt = stepGain * _blend * aimScale * w * Mathf.Lerp(0.6f, 1f, speedNorm);
            _stepVel += new Vector3(((stepIndex & 1) == 0 ? -0.0025f : 0.0025f) * jolt, -0.006f * jolt, 0f);
            _stepEulerVel += new Vector3(-1.1f * jolt, 0f, ((stepIndex & 1) == 0 ? -0.6f : 0.6f) * jolt);
        }
        // Quick thud spring — near-critical so each step reads as one dip,
        // not a ring that feeds back into weapon motion.
        _stepVel += (-_stepOffset * 260f - _stepVel * 30f) * dt;
        _stepOffset += _stepVel * dt;
        _stepEulerVel += (-_stepEuler * 220f - _stepEulerVel * 28f) * dt;
        _stepEuler += _stepEulerVel * dt;

        // --- Breathing: chest rise/fall, deeper and faster when exerted or
        // under adrenaline. The cycle comes from BreathingSystem so the
        // motion lines up with the breathing you hear.
        float breathGain = HandlingConfig.HeadBreath?.Value ?? 0f;
        float breathAmp = breathGain * Mathf.Lerp(0.35f, 1.4f, Mathf.Max(exertionGain, BreathingSystem.Depth));
        float bIn = Mathf.Sin(BreathingSystem.Phase);
        float bOut = 1f - Mathf.Cos(BreathingSystem.Phase);
        Vector3 breathPos = new(0f, bOut * 0.0035f * breathAmp, bOut * 0.0015f * breathAmp);
        float breathPitch = bIn * 0.28f * breathAmp;
        float breathRoll = (Mathf.PerlinNoise(Time.time * 0.13f, 7.7f) - 0.5f) * 0.35f * breathAmp;

        // --- ADS settle: small sight-picture drift converging over ~1s ---
        bool aiming = HandlingState.IsAiming;
        float settleGain = HandlingConfig.HeadAdsSettle?.Value ?? 0f;
        if (aiming && !_wasAiming)
        {
            _adsSettle = new Vector3(
                (Mathf.PerlinNoise(Time.time * 3.1f, 31.7f) - 0.5f) * 2.4f,
                (Mathf.PerlinNoise(47.3f, Time.time * 3.7f) - 0.5f) * 1.6f,
                0f) * settleGain;
        }
        _wasAiming = aiming;
        _adsSettle = Vector3.Lerp(_adsSettle, Vector3.zero, 1f - Mathf.Exp(-dt / 1.1f));

        // --- Muzzle jolt spring (camera-local position kick from firing) ---
        if (_lastPosKickFrame != Time.frameCount)
        {
            _lastPosKickFrame = Time.frameCount;
            float kdt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            _posKickVelocity += (-_posKick * 400f - _posKickVelocity * 26f) * kdt;
            _posKick += _posKickVelocity * kdt;
            if (_posKick.sqrMagnitude < 1e-10f && _posKickVelocity.sqrMagnitude < 1e-10f)
            {
                _posKick = Vector3.zero;
                _posKickVelocity = Vector3.zero;
            }
        }

        Vector3 basePosition = camera.localPosition;
        if ((basePosition - _lastResultPos).sqrMagnitude < 1e-10f)
            basePosition -= _lastAppliedDelta;

        Vector3 motionDelta = _stepOffset + breathPos + _posKick;
        Vector3 euler = _stepEuler + new Vector3(breathPitch, 0f, breathRoll) + _adsSettle;
        euler.x = Mathf.Clamp(euler.x, -MaxRotationDegrees, MaxRotationDegrees);
        euler.y = Mathf.Clamp(euler.y, -MaxRotationDegrees, MaxRotationDegrees);
        euler.z = Mathf.Clamp(euler.z, -MaxRollDegrees, MaxRollDegrees);

        // Low-pass the final deltas: the weapon's lazy spring chases a
        // camera-relative target, so raw frame-to-frame impulse noise reads
        // as gun jitter. ~18Hz smoothing keeps the thud, kills the buzz.
        float sm = 1f - Mathf.Exp(-14f * dt);
        _posDeltaSm = Vector3.Lerp(_posDeltaSm, motionDelta, sm);
        _eulerSm = Vector3.Lerp(_eulerSm, euler, sm);
        euler = _eulerSm;

        Vector3 positionDelta = Vector3.ClampMagnitude(_posDeltaSm, MaxPositionOffset);
        _lastAppliedDelta = positionDelta;
        camera.localPosition = basePosition + positionDelta;
        _lastResultPos = camera.localPosition;

        LastPosDelta = _posDeltaSm;
        LastEulerDelta = euler;
        if (euler.sqrMagnitude > 0.000001f)
            camera.localRotation = Quaternion.Euler(euler) * camera.localRotation;

        _lastApplyFrame = Time.frameCount;
    }

    public static void Reset()
    {
        _initialized = false;
        _lastAppliedDelta = Vector3.zero;
        _lastResultPos = Vector3.zero;
        _shotShake = Vector3.zero;
        _shotShakeVelocity = Vector3.zero;
        _posKick = Vector3.zero;
        _posKickVelocity = Vector3.zero;
        _lastPosKickFrame = -1;
        _stridePhase = 0f;
        _stepSide = 0;
        _stepOffset = Vector3.zero;
        _stepVel = Vector3.zero;
        _stepEuler = Vector3.zero;
        _stepEulerVel = Vector3.zero;
        _exertion = 0f;
        _adsSettle = Vector3.zero;
        _wasAiming = false;
        _lastApplyFrame = -1;
        _posDeltaSm = Vector3.zero;
        _eulerSm = Vector3.zero;
        LastPosDelta = Vector3.zero;
        LastEulerDelta = Vector3.zero;
        _directionLag = Vector3.zero;
        _weaponCoupling = Vector3.zero;
        _injuryDrift = Vector3.zero;
        _injuryTime = 0f;
        _lastShakeFrame = -1;
        _blend = 1f;
    }
}
