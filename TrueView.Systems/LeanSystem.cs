using EFT;
using TrueView.Configuration;
using TrueView.Core;
using UnityEngine;

namespace TrueView.Systems;

internal static class LeanSystem
{
	private const float Frequency = 4.5f;

	private const float DampingRatio = 0.8f;

	private static float _lean;

	private static float _velocity;

	public static float Lean => _lean;

	public static void Tick(Player player, float deltaTime)
	{
		if (deltaTime <= 0f)
		{
			return;
		}
		deltaTime = Mathf.Min(deltaTime, 1f / 15f);

		float num = 0f;
		if (HandlingConfig.LeanEnabled.Value && HandlingState.HasWeapon && player != null)
		{
			float turn = Mathf.Clamp(MotionTracker.YawRateSlow / 180f, -1.5f, 1.5f);

			float yaw = player.Rotation.x * Mathf.Deg2Rad;
			float sin = Mathf.Sin(yaw);
			float cos = Mathf.Cos(yaw);
			float localRight = player.Velocity.x * cos - player.Velocity.z * sin;

			float inputLateral = MotionTracker.InputDirection.x;
			float movementLateral = Mathf.Clamp(localRight * 0.08f, -1.5f, 1.5f) * MotionTracker.NormalizedSpeed;
			float lateral = inputLateral * 0.6f + movementLateral;

			num = Mathf.Clamp(turn + lateral, -1.5f, 1.5f) * Mathf.Clamp01(HandlingConfig.LeanMultiplier.Value);
			if (HandlingState.IsAiming)
			{
				num *= 0.55f;
			}
		}
		float num3 = 20.25f;
		float num4 = 7.2000003f;
		_velocity += ((num - _lean) * num3 - _velocity * num4) * deltaTime;
		_lean += _velocity * deltaTime;

	}

	public static Vector3 Modify(Vector3 headRotation)
	{
		headRotation.z -= _lean * HandlingConfig.LeanCameraAngle.Value;
		return headRotation;
	}

	public static float WeaponRoll()
	{
		if (!HandlingConfig.LeanEnabled.Value)
		{
			return 0f;
		}
		return (0f - _lean) * HandlingConfig.LeanWeaponAngle.Value;
	}

	public static void Reset()
	{
		_lean = 0f;
		_velocity = 0f;
	}
}
