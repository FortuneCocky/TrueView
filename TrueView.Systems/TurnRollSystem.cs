using EFT;
using TrueView.Configuration;
using TrueView.Core;
using UnityEngine;

namespace TrueView.Systems;

/// <summary>
/// Deadzone extras, layered on top of the existing systems:
///  - Turn roll: turning banks the whole view like a body that swings into the
///    turn. Underdamped, so it overshoots and settles.
///  - ADS arc: the gun doesn't slide straight to the sights; it swings in on an
///    arc from low and to the side, rolling, then rings down onto the sights.
///    Leaving the sights drops it back with a smaller arc.
/// </summary>
internal static class TurnRollSystem
{
	private const float RollFrequency = 4.2f;
	private const float RollDamping = 0.42f;
	private const float ArcTime = 0.42f;

	private static float _roll;
	private static float _rollVel;
	private static bool _wasAiming;
	private static float _arcAge = 99f;
	private static float _arcDir = 1f;

	public static bool ArcActive => _arcAge < 1.3f;

	public static Vector3 ArcPos { get; private set; }

	public static Quaternion ArcRot { get; private set; } = Quaternion.identity;

	public static void Tick(Player player, float deltaTime)
	{
		if (deltaTime <= 0f || HandlingConfig.TurnRoll == null)
		{
			return;
		}
		deltaTime = Mathf.Min(deltaTime, 1f / 15f);
		bool on = HandlingState.HasWeapon && player != null; // each slider at 0 is off

		// --- roll from turning ---
		float target = 0f;
		if (on)
		{
			float turn = Mathf.Clamp(MotionTracker.YawRateSlow / 150f, -1.5f, 1.5f);
			target = turn * HandlingConfig.TurnRoll.Value;
			if (HandlingState.IsAiming)
			{
				target *= 0.35f;
			}
		}
		float w = 2f * Mathf.PI * RollFrequency;
		_rollVel += ((target - _roll) * w * w - _rollVel * 2f * RollDamping * w) * deltaTime;
		_roll = Mathf.Clamp(_roll + _rollVel * deltaTime, -30f, 30f);

		// --- ADS arc ---
		bool aiming = on && HandlingState.IsAiming;
		if (aiming != _wasAiming)
		{
			_arcAge = 0f;
			_arcDir = aiming ? 1f : 0.5f;
			_wasAiming = aiming;
		}
		if (_arcAge < 1.3f)
		{
			_arcAge += deltaTime;
		}
		float s = on ? HandlingConfig.AdsSwingArc.Value : 0f;
		float b;
		if (_arcAge < ArcTime)
		{
			b = Mathf.Sin(Mathf.PI * _arcAge / ArcTime);
		}
		else
		{
			float u = _arcAge - ArcTime;
			b = -0.22f * Mathf.Exp(-7f * u) * Mathf.Cos(2f * Mathf.PI * 4.5f * u);
		}
		b *= s * _arcDir;
		ArcPos = new Vector3(0.045f * b, -0.035f * b, -0.015f * b);
		ArcRot = Quaternion.Euler(-5f * b, -2f * b, 9f * b);
	}

	/// <summary>Camera roll, same sign convention as LeanSystem (banks into the turn).</summary>
	public static Vector3 Modify(Vector3 headRotation)
	{
		headRotation.z -= _roll;
		return headRotation;
	}

	public static void Reset()
	{
		_roll = 0f;
		_rollVel = 0f;
		_wasAiming = false;
		_arcAge = 99f;
		ArcPos = Vector3.zero;
		ArcRot = Quaternion.identity;
	}
}
