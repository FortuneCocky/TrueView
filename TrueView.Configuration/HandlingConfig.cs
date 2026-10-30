using BepInEx.Configuration;

namespace TrueView.Configuration;

internal sealed class ConfigurationManagerAttributes
{
	public int? Order { get; set; }
	public bool? IsAdvanced { get; set; }
}

internal static class HandlingConfig
{
	private static ConfigDescription Toggle(string description) => new(description, null, new ConfigurationManagerAttributes { Order = 1000 });
	private const string GeneralSection = "01 - General";

	private const string AimingSection = "05 - Aiming Speed & Weight";

	private const string SwaySection = "06 - Weapon Sway & Inertia";

	private const string DeadzoneSection = "04 - ADS Weapon Handling";

	private const string ShotSection = "07 - Recoil & Shot Response";

	private const string CameraSection = "08 - Camera Sway";

	private const string LeanSection = "12 - Turn Lean";

	private const string MomentumSection = "11 - Body Momentum";

	private const string FluidSection = "13 - Fluid Movement";

	public static ConfigEntry<bool> Enabled { get; private set; }

	public static ConfigEntry<bool> VerboseLogging { get; private set; }

	public static ConfigEntry<bool> SprintCancelAds { get; private set; }

	public static ConfigEntry<float> FreeLookReturnSpeed { get; private set; }

	public static ConfigEntry<bool> AimingEnabled { get; private set; }

	public static ConfigEntry<float> LightWeightKg { get; private set; }

	public static ConfigEntry<float> HeavyWeightKg { get; private set; }

	public static ConfigEntry<float> AdsTimeLight { get; private set; }

	public static ConfigEntry<float> AdsTimeHeavy { get; private set; }

	public static ConfigEntry<float> ErgonomicsInfluence { get; private set; }

	public static ConfigEntry<float> StaminaAdsInfluence { get; private set; }

	public static ConfigEntry<bool> SwayEnabled { get; private set; }

	public static ConfigEntry<float> SwayMultiplier { get; private set; }

	public static ConfigEntry<float> HipSwayMultiplier { get; private set; }

	public static ConfigEntry<float> AimSwayMultiplier { get; private set; }

	public static ConfigEntry<float> MotionInertiaMultiplier { get; private set; }

	public static ConfigEntry<float> StaminaSwayInfluence { get; private set; }

	public static ConfigEntry<float> TremorMultiplier { get; private set; }

	public static ConfigEntry<bool> DeadzoneEnabled { get; private set; }

	public static ConfigEntry<float> DeadzoneAds { get; private set; }

	public static ConfigEntry<float> AdsFollowTightness { get; private set; }

	public static ConfigEntry<bool> HipDeadzoneEnabled { get; private set; }

	public static ConfigEntry<float> HipCameraFollowZone { get; private set; }


	public static ConfigEntry<float> HipDeadzoneInfluence { get; private set; }
	public static ConfigEntry<float> HipDeadzoneSmoothing { get; private set; }

	public static ConfigEntry<float> HipNaturalMotion { get; private set; }

	public static ConfigEntry<float> HipOrganicDrift { get; private set; }

	public static ConfigEntry<float> HipMouseWhip { get; private set; }

	public static ConfigEntry<float> HipDirectionalTilt { get; private set; }

	// Procedural arm/wrist motion layers for hip fire
	public static ConfigEntry<float> HipStrafeRoll { get; private set; }
	public static ConfigEntry<float> HipAccelSway { get; private set; }
	public static ConfigEntry<float> HipWristRoll { get; private set; }
	public static ConfigEntry<float> HipBodyTurnLead { get; private set; }
	public static ConfigEntry<float> HipTurnBend { get; private set; }
	public static ConfigEntry<float> TurnConeExpand { get; private set; }
	public static ConfigEntry<float> AdsTurnBend { get; private set; }
	public static ConfigEntry<float> AdsWeaponLead { get; private set; }
	public static ConfigEntry<float> AdsDynamicCant { get; private set; }

	public static ConfigEntry<bool> ShotMisalignmentEnabled { get; private set; }

	public static ConfigEntry<float> ShotMisalignmentStrength { get; private set; }

	public static ConfigEntry<float> ShotMisalignmentMax { get; private set; }

	public static ConfigEntry<float> ShotBreathPenalty { get; private set; }

	public static ConfigEntry<float> StaminaShotInfluence { get; private set; }

	public static ConfigEntry<bool> CameraLagEnabled { get; private set; }

	public static ConfigEntry<float> CameraSwayFollow { get; private set; }

	public static ConfigEntry<float> CameraSwayFollowSpeed { get; private set; }

	public static ConfigEntry<float> CameraSettleSmoothing { get; private set; }

	public static ConfigEntry<bool> LeanEnabled { get; private set; }

	public static ConfigEntry<float> LeanCameraAngle { get; private set; }

	public static ConfigEntry<float> LeanWeaponAngle { get; private set; }

	public static ConfigEntry<float> LeanMultiplier { get; private set; }

	// Procedural Q/E lean: extra roll from the vanilla lean input

	public static ConfigEntry<bool> GaitEnabled { get; private set; }

	public static ConfigEntry<float> GaitIntensity { get; private set; }

	public static ConfigEntry<float> GaitTransitionSpeed { get; private set; }

	public static ConfigEntry<bool> InjuryHandlingEnabled { get; private set; }

	public static ConfigEntry<float> InjuryHandlingStrength { get; private set; }

	public static ConfigEntry<bool> InjuryPainkillerMasking { get; private set; }

	public static ConfigEntry<bool> MomentumEnabled { get; private set; }

	public static ConfigEntry<float> MomentumAmount { get; private set; }

	public static ConfigEntry<float> MomentumWeight { get; private set; }

	public static ConfigEntry<float> MomentumTurn { get; private set; }

	public static ConfigEntry<float> MomentumStopDelay { get; private set; }

	public static ConfigEntry<float> MomentumWalkForwardLean { get; private set; }

	public static ConfigEntry<float> MomentumWalkSideLean { get; private set; }

	// Fluid Movement — landing dip, crouch dip
	public static ConfigEntry<bool> HeadAttachEnabled { get; private set; }
	public static ConfigEntry<float> HeadStepImpact { get; private set; }
	public static ConfigEntry<float> HeadBreath { get; private set; }
	public static ConfigEntry<float> HeadExertion { get; private set; }
	public static ConfigEntry<float> HeadAdsSettle { get; private set; }
	public static ConfigEntry<float> HeadDirectionLag { get; private set; }
	public static ConfigEntry<float> HeadWeaponCoupling { get; private set; }
	public static ConfigEntry<float> HeadShotShake { get; private set; }
	public static ConfigEntry<float> HeadShotShakeRecovery { get; private set; }
	public static ConfigEntry<bool> FluidMovementEnabled { get; private set; }
	public static ConfigEntry<float> FluidAimReduction { get; private set; }
	public static ConfigEntry<float> FluidLandingDip { get; private set; }
	public static ConfigEntry<float> FluidLandingRecovery { get; private set; }
	public static ConfigEntry<float> FluidCrouchDip { get; private set; }
	public static ConfigEntry<float> FluidCrouchSpeed { get; private set; }

	public static ConfigEntry<bool> TurnInPlaceEnabled { get; private set; }
	public static ConfigEntry<float> TurnInPlaceThreshold { get; private set; }
	public static ConfigEntry<float> TurnInPlaceIntensity { get; private set; }

	public static ConfigEntry<bool> HitReactionEnabled { get; private set; }
	public static ConfigEntry<float> HitReactionStrength { get; private set; }
	public static ConfigEntry<bool> MuzzleJoltEnabled { get; private set; }
	public static ConfigEntry<float> MuzzleJoltAmount { get; private set; }
	public static ConfigEntry<bool> DynamicFovEnabled { get; private set; }
	public static ConfigEntry<float> DynamicFovAds { get; private set; }

	public static ConfigEntry<bool> LazyPhysicsEnabled { get; private set; }
	public static ConfigEntry<float> LazySpringiness { get; private set; }
	public static ConfigEntry<float> LazyTension { get; private set; }
	public static ConfigEntry<float> LazyWeightInfluence { get; private set; }
	public static ConfigEntry<float> LazyPosLag { get; private set; }

	public static ConfigEntry<bool> AdrenalineEnabled { get; private set; }
	public static ConfigEntry<float> AdrenalineRadius { get; private set; }
	public static ConfigEntry<float> AdrenalineTremor { get; private set; }
	public static ConfigEntry<float> AdrenalineFovPinch { get; private set; }
	public static ConfigEntry<float> AdrenalineHeartbeat { get; private set; }
	public static ConfigEntry<float> AdrenalineDecay { get; private set; }
	public static ConfigEntry<float> BreathingVolume { get; private set; }

	public static ConfigEntry<bool> HardDeadzone { get; private set; }
	public static ConfigEntry<float> TurnRoll { get; private set; }
	public static ConfigEntry<float> AdsSwingArc { get; private set; }



	private static ConfigDescription D(string desc, float min, float max, bool advanced = true)
	{
		return new ConfigDescription(desc, new AcceptableValueRange<float>(min, max),
			new ConfigurationManagerAttributes { IsAdvanced = advanced });
	}

	public static void Init(ConfigFile config)
	{
		// framework drive footsteps, landing, leans and per-shot recoil ---


		Enabled = config.Bind("01 - General", "Enable mod", true, Toggle("Master switch. When off, all handling systems leave the vanilla values untouched."));
		VerboseLogging = config.Bind("01 - General", "Verbose logging", false, "Log recalculated weapon handling values to the BepInEx console.");
		SprintCancelAds = config.Bind("01 - General", "Cancel sprint to ADS on right click", true, "Pressing right mouse while sprinting cancels sprint and immediately aims down sights.");
		FreeLookReturnSpeed = config.Bind("01 - General", "Free look return speed", 12f, D("How fast the camera smoothly returns to forward view after exiting free look. Higher = snappier, lower = smoother. 0 = instant snap.", 0f, 30f, true));
		HeadAttachEnabled = config.Bind("02 - Head Camera", "Enable head camera", true, Toggle("Attaches the camera to the character's head so the view follows body motion (steps, crouching, leaning, landing)."));
		HeadStepImpact = config.Bind("02 - Head Camera", "Footstep jolt", 1f, D("Per-step camera thud — a quick downward jolt with alternating roll on each foot strike, scaled by speed and weapon weight. 0 = off, 0.8 = natural, 2 = strong.", 0f, 2f, true));
		HeadBreath = config.Bind("02 - Head Camera", "Breathing intensity", 0.1032864f, D("Subtle chest/head rise and fall at rest. Gets deeper and faster with exertion. 0 = off, 1 = natural.", 0f, 2f, true));
		HeadExertion = config.Bind("02 - Head Camera", "Exertion response", 1f, D("How much low stamina deepens and quickens breathing and amplifies steps. 0 = breathing stays calm, 2 = heavy post-sprint recovery.", 0f, 2f, true));
		HeadAdsSettle = config.Bind("02 - Head Camera", "ADS settle drift", 0.4f, D("Tiny sight-picture drift that converges over ~1 second after raising the gun. 0 = locked instantly, 1 = strong settle.", 0f, 1f, true));
		HeadDirectionLag = config.Bind("02 - Head Camera", "Direction change lag", 0f, D("Physical head lag and follow-through during rapid turns and direction changes. 0 = off, 1 = strong.", 0f, 1f, true));
		HeadWeaponCoupling = config.Bind("02 - Head Camera", "Weapon to head coupling", 0f, D("How much shouldered weapon movement transfers into the head camera. Stronger in ADS. 0 = independent, 1 = strongly coupled.", 0f, 1f, true));
		HeadShotShake = config.Bind("02 - Head Camera", "Shot shake amount", 1.3f, D("Smooth head-mounted camera kick from firing. 0 = off, 1 = natural, 2 = strong.", 0f, 2f, true));
		HeadShotShakeRecovery = config.Bind("02 - Head Camera", "Shot shake recovery", 10f, D("How quickly firing shake settles. Lower = heavier and floatier, higher = tighter.", 2f, 20f, true));
		HipDeadzoneEnabled = config.Bind("03 - Deadzone", "Enable hip deadzone", true, Toggle("Hip deadzone. The camera follows player input while the weighted weapon trails behind it."));
		HipCameraFollowZone = config.Bind("03 - Deadzone", "Weapon lag limit (deg)", 14.5f, new ConfigDescription("Maximum angle the weapon can trail behind the camera during hip fire.", new AcceptableValueRange<float>(1f, 30f)));
		HipDeadzoneInfluence = config.Bind("03 - Deadzone", "Deadzone influence", 0.2535211f, D("How much player movement/turning adds procedural drift to the weapon. 0 = none, 1 = maximum sway.", 0f, 1f, true));
		HipDeadzoneSmoothing = config.Bind("03 - Deadzone", "Weapon follow speed", 1f, D("How quickly the weighted weapon catches up with the player-driven camera. 0 = heavy/slow, 1 = responsive/fast.", 0f, 1f, true));
		HipNaturalMotion = config.Bind("03 - Deadzone", "Natural motion", 1.1f, new ConfigDescription("Overall strength of human-like weapon motion at hip. Adds organic drift, mouse whip and directional tilt for an immersive feel. 0 = off, 1 = natural, higher = more pronounced.", new AcceptableValueRange<float>(0f, 3f)));
		HipOrganicDrift = config.Bind("03 - Deadzone", "Organic drift", 0.4f, D("Perlin-noise micro-drift simulating hands holding a weapon. 0 = robotic/still, 1 = subtle human tremor, 2 = heavy drift.", 0f, 2f, true));
		HipMouseWhip = config.Bind("03 - Deadzone", "Mouse whip", 1f, D("How much the weapon whips and lags behind fast mouse movements, then springs back. 0 = rigid, 1 = natural, 2 = heavy lag.", 0f, 3f, true));
		HipDirectionalTilt = config.Bind("03 - Deadzone", "Directional tilt", 1.547887f, D("Weapon roll/tilt in the direction of mouse motion, like wrists flexing. 0 = none, 1 = subtle, 2 = pronounced.", 0f, 3f, true));
		HipStrafeRoll = config.Bind("03 - Deadzone", "Strafe wrist roll", 1f, D("Weapon rolls (wrist rotation) when strafing left/right, as if the arms swing with body momentum. 0 = off, 1 = natural, 2 = pronounced.", 0f, 3f, true));
		HipAccelSway = config.Bind("03 - Deadzone", "Acceleration sway", 1.6f, D("Weapon pitches forward/back when starting or stopping movement, simulating arm inertia. 0 = off, 1 = natural, 2 = heavy.", 0f, 3f, true));
		HipWristRoll = config.Bind("03 - Deadzone", "Idle wrist roll", 0.7f, D("Subtle random wrist roll while holding the weapon at hip, simulating micro-adjustments of the grip. 0 = off, 1 = noticeable.", 0f, 2f, true));
		HipBodyTurnLead = config.Bind("03 - Deadzone", "Body turn lead", 2.535211f, D("Wrist-turn lead: when you move the mouse, the muzzle swings ahead of the turn direction and the gun banks like your wrist is twisting, then springs back to neutral. 0 = off, 1 = natural, 3 = dramatic.", 0f, 3f, true));
		HipTurnBend = config.Bind("03 - Deadzone", "Weapon bend on turn", 1.619718f, D("Muzzle lead: turning makes the gun pivot in your hands — the muzzle throws out ahead of the turn (harder on flicks), banks, then swings back through center with a slight overshoot on its own. Heavier guns swing wider and slower. 0 = off, 1 = natural, 3 = dramatic.", 0f, 6f, true));
		TurnConeExpand = config.Bind("03 - Deadzone", "Turn range expansion", 1.616901f, D("How much the weapon's range of motion grows while turning. 0 = fixed cone (gun can never trail further than the deadzone), 1 = cone doubles at full turn speed — the gun sweeps wide and can 'point around the corner' mid-turn, 2 = extreme.", 0f, 2f, true));
		TurnRoll = config.Bind("03 - Deadzone", "Turn roll (deg)", 6f, D("How far the whole view banks into a fast turn (underdamped: it overshoots and settles). 0 = off, 6 = noticeable, 12 = very dramatic. Stacks with Turn Lean.", 0f, 20f, false));
		AdsSwingArc = config.Bind("03 - Deadzone", "ADS swing-in arc", 0.35f, D("The gun swings in on an arc (from low and to the side, rolling) instead of sliding straight to the sights, then rings down onto them. 0 = off, 1 = natural, 2 = dramatic. Negative values mirror the arc direction.", -2f, 2f, false));
		HardDeadzone = config.Bind("03 - Deadzone", "Keep gun on screen", true, Toggle("Hard deadzone: the gun's total swing (lag + wrist lead + muzzle bend) is smoothly limited to the lag-limit degrees, so it never turns off screen during fast turns."));
		AdsTurnBend = config.Bind("04 - ADS Weapon Handling", "Weapon bend while aiming", 0.3239437f, D("Fraction of the hip 'Weapon bend on turn' applied while aiming down sights — the muzzle leads the turn then straightens, scaled down so the sight stays usable. 0 = rigid ADS, 1 = same bend as hip.", 0f, 1.5f, true));
		AdsWeaponLead = config.Bind("04 - ADS Weapon Handling", "ADS weapon lead", 1f, D("While aiming, mouse turns twist the gun in your hands like the hip 'Body turn lead': the muzzle swings out ahead of the turn and the gun banks, then springs back. Pivots at the sight so the dot stays usable. 0 = off (rigid ADS), 1 = natural, 3 = dramatic.", 0f, 3f, false));
		AdsDynamicCant = config.Bind("04 - ADS Weapon Handling", "Dynamic cant", 1f, D("How much turning, strafing and hand wander change the cant while aiming, so the gun is never locked level. 0 = static cant, 1 = natural, 2 = loose.", 0f, 2f, false));
		DeadzoneEnabled = config.Bind("04 - ADS Weapon Handling", "Enable ADS deadzone", true, Toggle("ADS deadzone. The camera stays player-driven while the weapon trails toward the selected sight."));
		DeadzoneAds = config.Bind("04 - ADS Weapon Handling", "ADS lag limit (deg)", 12f, new ConfigDescription("Maximum weapon-to-camera separation while aiming down sights.", new AcceptableValueRange<float>(0f, 12f)));
		AdsFollowTightness = config.Bind("04 - ADS Weapon Handling", "ADS follow tightness", 0.3826291f, D("How tightly the aimed weapon follows the camera. 0 = heavy delayed catch-up, 1 = complete immediate follow. Weapon weight, ergonomics, movement and fatigue are calculated automatically.", 0f, 1f, true));
		AimingEnabled = config.Bind("05 - Aiming Speed & Weight", "Enable aiming changes", true, Toggle("Scale ADS speed by weapon weight, ergonomics and hands stamina."));
		LightWeightKg = config.Bind("05 - Aiming Speed & Weight", "Light weapon weight (kg)", 2f, D("Weight treated as a light weapon.", 0.5f, 8f, true));
		HeavyWeightKg = config.Bind("05 - Aiming Speed & Weight", "Heavy weapon weight (kg)", 10f, D("Weight treated as a heavy weapon.", 3f, 20f, true));
		AdsTimeLight = config.Bind("05 - Aiming Speed & Weight", "ADS time, light weapon (s)", 1f, D("Time to fully aim a light, high ergonomics weapon.", 0.05f, 2f, true));
		AdsTimeHeavy = config.Bind("05 - Aiming Speed & Weight", "ADS time, heavy weapon (s)", 1f, D("Time to fully aim a heavy, high ergonomics weapon.", 0.1f, 3f, true));
		ErgonomicsInfluence = config.Bind("05 - Aiming Speed & Weight", "Ergonomics influence", 1f, D("How much low ergonomics adds to the ADS time. 0 disables the ergonomics term.", 0f, 2f, true));
		StaminaAdsInfluence = config.Bind("05 - Aiming Speed & Weight", "Hands stamina influence", 1f, D("How much drained hands stamina slows aiming down.", 0f, 1f, true));
		SwayEnabled = config.Bind("06 - Weapon Sway & Inertia", "Enable sway changes", true, Toggle("Scale sway, weapon inertia and tremor by weight, ergonomics and stamina."));
		SwayMultiplier = config.Bind("06 - Weapon Sway & Inertia", "Sway multiplier", 0.2f, D("Scales the ergonomic weight that drives weapon sway and recoil behaviour. 0 = none, 2 = double.", 0f, 2f, true));
		AimSwayMultiplier = config.Bind("06 - Weapon Sway & Inertia", "Aim sway multiplier", 0.15f, D("Breath/sway strength while aiming. 0 = none, 2 = double.", 0f, 2f, true));
		MotionInertiaMultiplier = config.Bind("06 - Weapon Sway & Inertia", "Weapon inertia multiplier", 2.6f, D("How much the weapon lags behind player movement and mouse motion.", 0.1f, 4f, true));
		StaminaSwayInfluence = config.Bind("06 - Weapon Sway & Inertia", "Stamina influence", 1f, D("How much drained stamina and oxygen add to sway.", 0f, 2f, true));
		TremorMultiplier = config.Bind("06 - Weapon Sway & Inertia", "Tremor multiplier", 0.1f, D("Multiplier on hand shake / tremor intensity.", 0.1f, 4f, true));
		ShotMisalignmentEnabled = config.Bind("07 - Recoil & Shot Response", "Enable shot misalignment", true, Toggle("Every shot nudges the weapon out of alignment, scaled by weight and ergonomics."));
		HipSwayMultiplier = config.Bind("07 - Recoil & Shot Response", "Recoil intensity", 0.3286385f, D("How chaotic recoil is while hip firing. 0 = vanilla-calm, 1 = very chaotic.", 0f, 1f, true));
		ShotMisalignmentStrength = config.Bind("07 - Recoil & Shot Response", "Strength", 1.2f, D("Impulse applied to the hands rotation spring per shot.", 0f, 4f, true));
		ShotMisalignmentMax = config.Bind("07 - Recoil & Shot Response", "Maximum per shot (deg)", 1.5f, D("Clamp on a single shot's misalignment impulse.", 0.1f, 10f, true));
		ShotBreathPenalty = config.Bind("07 - Recoil & Shot Response", "Accuracy loss per shot", 0.95f, D("Multiplier on the vanilla breath amplitude gain applied per shot.", 0f, 3f, true));
		StaminaShotInfluence = config.Bind("07 - Recoil & Shot Response", "Stamina recoil influence", 3f, D("How much drained hands stamina increases shot misalignment and recoil shake.", 0f, 3f, true));
		CameraLagEnabled = config.Bind("08 - Camera Sway", "Enable camera lag", false, Toggle("The head/camera trails the weapon's sway instead of sitting rigidly on it."));
		CameraSwayFollow = config.Bind("08 - Camera Sway", "Sway follow amount", 0.4037559f, D("Fraction of the weapon sway the camera drifts after.", 0f, 1f, true));
		CameraSwayFollowSpeed = config.Bind("08 - Camera Sway", "Sway follow speed", 11.8f, D("How fast the camera catches up with the weapon sway. Lower lags further behind.", 0.5f, 20f, true));
		CameraSettleSmoothing = config.Bind("08 - Camera Sway", "Hip camera smoothing", 2f, D("Multiplier on the vanilla non-aiming camera settle speed. Below 1 is softer.", 0.2f, 2f, true));
		GaitEnabled = config.Bind("09 - Locomotion Gaits", "Enable gait system", true, Toggle("Adds smooth posture-specific movement rhythms and handling changes for walking, jogging, sprinting, crouching and prone movement."));
		GaitIntensity = config.Bind("09 - Locomotion Gaits", "Gait intensity", 1f, D("Overall strength of gait-driven camera and weapon movement. 0 = off, 1 = natural, 2 = pronounced.", 0f, 2f, true));
		GaitTransitionSpeed = config.Bind("09 - Locomotion Gaits", "Gait transition speed", 6f, D("How quickly the body blends between movement gaits. Lower = heavier, higher = more responsive.", 1f, 12f, true));
		InjuryHandlingEnabled = config.Bind("10 - Injury-Driven Handling", "Enable injury handling", true, Toggle("Makes arm, leg, torso and head damage physically affect weapon control, movement and camera stability."));
		InjuryHandlingStrength = config.Bind("10 - Injury-Driven Handling", "Injury handling strength", 1f, D("Overall strength of physical injury effects. 0 = off, 1 = natural, 2 = severe.", 0f, 2f, true));
		InjuryPainkillerMasking = config.Bind("10 - Injury-Driven Handling", "Painkillers mask handling", true, Toggle("Painkillers partially reduce perceived handling penalties without restoring damaged body-part health."));
		MomentumEnabled = config.Bind("11 - Body Momentum", "Enable player momentum", true, Toggle("Adds weight and inertia-driven camera/weapon sway for more realistic movement."));
		MomentumAmount = config.Bind("11 - Body Momentum", "Momentum amount", 1f, D("Overall strength of the momentum effect. 0 is off, 1 is natural.", 0f, 1f, true));
		MomentumWeight = config.Bind("11 - Body Momentum", "Inventory weight influence", 1f, D("How much inventory weight and inertia scale the effect. 0 is off, 1 is full.", 0f, 1f, true));
		MomentumTurn = config.Bind("11 - Body Momentum", "Turn momentum", 0.5492957f, D("Extra roll from turning left/right, scaled by weight. 0 is off, 1 is natural.", 0f, 1f, true));
		MomentumStopDelay = config.Bind("11 - Body Momentum", "Stop delay", 2f, D("How long the camera/weapon momentum lingers after the player stops moving. 0 is instant, 1 is heavy.", 0f, 2f, true));
		MomentumWalkForwardLean = config.Bind("11 - Body Momentum", "Walk forward/back lean", 2f, D("Camera pitch lean from walking forward and backward. Positive leans the camera with the movement.", -2f, 2f, true));
		MomentumWalkSideLean = config.Bind("11 - Body Momentum", "Walk side lean", 0.2f, D("Camera roll lean from strafing left and right. Positive leans the camera into the strafe.", -2f, 2f, true));
		LeanEnabled = config.Bind("12 - Turn Lean", "Enable turn lean", true, Toggle("Turning with the mouse leans the body into the turn, so the camera and weapon bank instead of rotating perfectly level."));
		LeanCameraAngle = config.Bind("12 - Turn Lean", "Camera lean (deg)", 2.5f, D("Maximum camera roll at the reference turn rate. Negative flips the direction of the lean.", -10f, 10f, true));
		LeanWeaponAngle = config.Bind("12 - Turn Lean", "Weapon lean (deg)", 0f, D("Maximum extra weapon roll at the reference turn rate. Negative flips the direction of the lean.", -15f, 15f, true));
		LeanMultiplier = config.Bind("12 - Turn Lean", "Lean amount", 1.5f, D("0 = no lean, 1 = natural, up to 3 for drastic lean.", 0f, 3f, true));
		// Procedural Q/E lean: adds extra camera and weapon roll when leaning with Q/E,
		// producing a more pronounced body-lean feel on top of the vanilla tilt.
		FluidMovementEnabled = config.Bind("13 - Fluid Movement", "Enable fluid movement", true, Toggle("Master toggle for landing dip and crouch dip. When off, vanilla camera behavior is untouched."));
		FluidAimReduction = config.Bind("13 - Fluid Movement", "Aim reduction", 0.4037559f, D("How much camera effects are reduced while aiming down sights. 0 = no reduction (full effects while aiming), 1 = fully suppressed while aiming.", 0f, 1f, true));
		FluidLandingDip = config.Bind("13 - Fluid Movement", "Landing dip", 1.5f, D("Camera dip intensity when landing from a fall. 0 = no dip, 1 = full dip proportional to fall duration.", 0f, 2f, true));
		FluidLandingRecovery = config.Bind("13 - Fluid Movement", "Landing recovery speed", 1.685915f, D("How quickly the landing dip springs back to neutral. Higher = faster recovery.", 1f, 10f, true));
		FluidCrouchDip = config.Bind("13 - Fluid Movement", "Crouch dip", 1f, D("Camera lower amount when crouching. 0 = no dip, 0.3 = subtle dip when entering crouch.", 0f, 1f, true));
		FluidCrouchSpeed = config.Bind("13 - Fluid Movement", "Crouch dip speed", 1.4f, D("How quickly the crouch dip transitions. Higher = snappier crouch dip.", 1f, 10f, true));
		TurnInPlaceEnabled = config.Bind("14 - Turn-in-Place Footwork", "Enable turn-in-place footwork", true, Toggle("While standing still, turns are absorbed by the waist first. Once the torso twist builds far enough, the feet reposition with a physical step that moves the camera and weapon."));
		TurnInPlaceThreshold = config.Bind("14 - Turn-in-Place Footwork", "Foot reposition angle (deg)", 45f, D("How far the torso can twist before the feet step to catch up. Lower = steps happen sooner, higher = more free waist turning.", 20f, 90f, true));
		TurnInPlaceIntensity = config.Bind("14 - Turn-in-Place Footwork", "Footwork intensity", 1.5f, D("Overall strength of twist and step effects on the camera and weapon. 0 = off, 1 = natural, 2 = pronounced.", 0f, 2f, true));
		HitReactionEnabled = config.Bind("15 - Combat Feedback", "Enable hit reaction", true, Toggle("Getting hit knocks your head camera the way the round travelled: pitched back from the front, turned and tilted from the side, harder for a head hit; a stomach hit doubles you forward, a leg hit dips the view. Bleeding, falls and the like don't."));
		HitReactionStrength = config.Bind("15 - Combat Feedback", "Hit reaction strength", 0.8f, new ConfigDescription("How hard a hit knocks the view (also scales with the damage). 0.5 = subtle, 1 = natural, 3 = violent.", new AcceptableValueRange<float>(0f, 3f), new ConfigurationManagerAttributes { IsAdvanced = true }));
		MuzzleJoltEnabled = config.Bind("15 - Combat Feedback", "Enable muzzle jolt", true, Toggle("Firing shoves the head camera straight back a few millimeters instead of only rotating it."));
		MuzzleJoltAmount = config.Bind("15 - Combat Feedback", "Muzzle jolt amount", 1f, new ConfigDescription("Strength of the translational camera kick on firing. 0.5 = subtle, 1 = natural, 3 = heavy.", new AcceptableValueRange<float>(0f, 3f), new ConfigurationManagerAttributes { IsAdvanced = true }));
		DynamicFovEnabled = config.Bind("15 - Combat Feedback", "Enable dynamic FOV", true, Toggle("Field of view narrows slightly while aiming down sights, plus a tunnel-vision pinch under adrenaline."));
		DynamicFovAds = config.Bind("15 - Combat Feedback", "ADS FOV change (%)", -2.15493f, new ConfigDescription("Percent FOV change while aiming. Negative = narrower (focus).", new AcceptableValueRange<float>(-15f, 15f), new ConfigurationManagerAttributes { IsAdvanced = true }));
		LazyPhysicsEnabled = config.Bind("16 - Lazy Weapon Physics", "Enable lazy weapon physics", true, Toggle("Replaces weapon camera-follow with a physical spring: the gun lags, overshoots and settles like a held object, and bounces on each footstep. Toggle off to A/B against the classic smoothed tracking."));
		LazySpringiness = config.Bind("16 - Lazy Weapon Physics", "Springiness", 0.6f, new ConfigDescription("How under-damped the weapon spring is. 0 = critically damped (no overshoot), 1 = natural wobble, 2 = rubber band.", new AcceptableValueRange<float>(0f, 2f), new ConfigurationManagerAttributes { IsAdvanced = true }));
		LazyTension = config.Bind("16 - Lazy Weapon Physics", "Muscle tension", 0.89f, new ConfigDescription("Stiffness multiplier on the weapon spring — how hard the arms pull the gun toward the camera. Lower = lazier, higher = tighter.", new AcceptableValueRange<float>(0.25f, 3f), new ConfigurationManagerAttributes { IsAdvanced = true }));
		LazyWeightInfluence = config.Bind("16 - Lazy Weapon Physics", "Weight influence", 1f, new ConfigDescription("How much weapon weight fights the spring. Heavy weapons get softer, less-damped arms — more lag and wobble. 0 = weight ignored, 1 = natural, 2 = exaggerated.", new AcceptableValueRange<float>(0f, 2f), new ConfigurationManagerAttributes { IsAdvanced = true }));
		LazyPosLag = config.Bind("16 - Lazy Weapon Physics", "Positional lag", 0.09295777f, new ConfigDescription("How much the gun's position (not just rotation) lags and overshoots — body dips, drift, settle. 0 = position tracks rigidly, 1 = fully sprung.", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { IsAdvanced = true }));


		AdrenalineEnabled = config.Bind("17 - Adrenaline", "Enable adrenaline response", true, Toggle("Nearby gunfire, your own shots, and hits taken raise an adrenaline level: hands tremor slightly, FOV pinches into tunnel vision, and a low heartbeat thumps faster and louder as the level climbs."));
		AdrenalineRadius = config.Bind("17 - Adrenaline", "Trigger radius (m)", 40f, D("Enemy gunfire within this distance feeds adrenaline; hits taken always count. Your own shots only sustain adrenaline once a fight is already active.", 5f, 150f, true));
		AdrenalineTremor = config.Bind("17 - Adrenaline", "Tremor amount", 0.8f, D("Extra hand-shake added on top of sway while adrenaline is up. 0 = none, 1 = noticeable wobble.", 0f, 3f, true));
		AdrenalineFovPinch = config.Bind("17 - Adrenaline", "FOV pinch (%)", 4f, D("Tunnel-vision FOV reduction at full adrenaline. 0 = none, 4 = subtle focus, 8 = strong tunnel.", 0f, 12f, true));
		AdrenalineHeartbeat = config.Bind("17 - Adrenaline", "Heartbeat volume", 0.5f, D("Volume of the synthesized heartbeat (rate and loudness scale with adrenaline). 0 = off.", 0f, 1f, true));
		AdrenalineDecay = config.Bind("17 - Adrenaline", "Decay time (s)", 8f, D("Seconds for adrenaline to fully drain after the last trigger.", 2f, 30f, true));
		BreathingVolume = config.Bind("17 - Adrenaline", "Breathing volume", 0.45f, D("Volume of your own breathing. Silent when calm with full stamina. Sprinting: running breaths that get louder as stamina runs out. Stamina under 50% or adrenaline: deep fast breathing, louder the emptier you are. Recovering from 50% to 85%: quiet nose breathing that fades out. 0 = off.", 0f, 1f, false));


	}
}
