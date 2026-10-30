using System.Reflection;
using EFT;
using HarmonyLib;
using TrueView.Configuration;
using TrueView.Core;
using TrueView.Systems;
using UnityEngine;
using SPT.Reflection.Patching;

namespace TrueView.Patches;

/// <summary>
/// The local player taking a hit: the head camera jolts away from the struck body part
/// (hit reaction) and adrenaline spikes. Damage that isn't a blow (bleeding, hunger, meds,
/// a fall) does neither.
/// </summary>
internal class Patch_ApplyDamageInfo : ModulePatch
{
	private const EDamageType Blows = EDamageType.Bullet | EDamageType.Sniper | EDamageType.Melee | EDamageType.Explosion
		| EDamageType.GrenadeFragment | EDamageType.Landmine | EDamageType.Blunt | EDamageType.Artillery
		| EDamageType.Btr | EDamageType.ThermobaricExplosion | EDamageType.Barbed;

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player), "ApplyDamageInfo");
	}

	// the damage info's type is EFT's own struct: read through __args rather than naming it
	[PatchPostfix]
	private static void Postfix(Player __instance, object[] __args)
	{
		if (__instance == null || !__instance.IsYourPlayer || !HandlingState.Enabled || __args == null || __args.Length < 2)
			return;
		Traverse info = Traverse.Create(__args[0]);
		EDamageType type = info.Field("DamageType").GetValue<EDamageType>();
		if ((type & Blows) == 0)
			return;
		float damage = info.Field("Damage").GetValue<float>();
		Vector3 direction = info.Field("Direction").GetValue<Vector3>();
		EBodyPart part = (EBodyPart)__args[1];
		AdrenalineSystem.OnHit();
		HeadAttachSystem.OnHitTaken(part, direction, damage, __instance);
	}
}
