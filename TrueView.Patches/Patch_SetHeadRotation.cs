using System.Reflection;
using EFT;
using EFT.Animations;
using TrueView.Core;
using TrueView.Systems;
using SPT.Reflection.Patching;
using UnityEngine;

namespace TrueView.Patches;

internal class Patch_SetHeadRotation : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(ProceduralWeaponAnimation).GetMethod("SetHeadRotation", BindingFlags.Instance | BindingFlags.Public);
	}

	[PatchPrefix]
	private static void Prefix(ProceduralWeaponAnimation __instance, ref Vector3 headRot)
	{
		if (!HandlingState.Enabled)
		{
			return;
		}
		Player player = HandlingManager.LocalPlayer();
		if (player == null || player.ProceduralWeaponAnimation != __instance)
		{
			return;
		}

		// No firearm in hands (eating, drinking, planting, meds, throwables,
		// empty hands) — pass the rotation through untouched so those
		// animations own the camera. Still record it so state stays sane.
		if (player.HandsController is not Player.FirearmController)
		{
			HandlingState.HeadRotation = headRot;
			return;
		}

		// Skip all camera modifications during free look — let vanilla handle it
		if (player.MouseLookControl)
		{
			HandlingState.HeadRotation = headRot;
			return;
		}

		// Skip deadzone/camera modifications during free-look return smoothing.
		// HandlingManager.Tick() sets HandlingState.HeadRotation directly
		// during the smoothing transition.
		if (HandlingManager.IsFreeLookReturnActive)
		{
			return;
		}

		// Base rotation: ProceduralHipSystem owns the weapon deadzone for both
		// hip and ADS, detaching the camera from the raw mouse rotation.
		Vector3 baseRot = ProceduralHipSystem.Modify(headRot);

		if (ProceduralHipSystem.ControlsCamera)
		{
			headRot = baseRot;
		}
		else
		{
			// Apply momentum, camera lag, and lean on top of the detached camera.
			headRot = TurnRollSystem.Modify(LeanSystem.Modify(CameraLagSystem.Modify(MomentumSystem.Modify(baseRot))));

			// Apply fluid movement camera bob (walk bob, landing dip, crouch dip).
			headRot = FluidMovementSystem.Modify(headRot);
		}

		headRot = LocomotionGaitSystem.ModifyCamera(headRot);
		headRot = TurnInPlaceSystem.ModifyCamera(headRot);
		headRot = HeadAttachSystem.ApplyShotShake(headRot);
		HandlingState.HeadRotation = headRot;
	}
}