using UnityEngine;

[CreateAssetMenu(
    fileName = "JuiceTimelineAsset",
    menuName = "Juice/Juice Timeline Asset")]
public class JuiceTimelineAsset : ScriptableObject
{
    [Header("ID / Label (for your own use)")]
    public string timelineId = "Default";

    [Header("Phase Durations")]
    [Tooltip("Pre-break tension phase before the snap (barrier hold).")]
    public float preHoldDuration = 0.15f;

    [Tooltip("Hard freeze frame at impact.")]
    public float freezeDuration = 0.06f;

    [Tooltip("Time it takes to ease into slow motion after the freeze.")]
    public float slowMoInDuration = 0.18f;

    [Tooltip("Time we stay at full slow motion before easing out.")]
    public float slowMoSustainDuration = 0.10f;

    [Tooltip("Time it takes to return from slow motion back to normal time.")]
    public float slowMoOutDuration = 0.35f;

    [Tooltip("Time it takes for camera push, FOV, blur, etc. to fully return.")]
    public float settleDuration = 0.25f;

    [Header("Module Toggles")]
    public bool useCameraPush = true;
    public bool useSlowMo = true;
    public bool useFreezeFrame = true;
    public bool useShake = true;
    public bool useFOVBurst = true;
    public bool useBloomPulse = true;
    public bool useChromaticFlash = true;
    public bool useMotionBlurSpike = true;
    public bool useRumble = true;
    public bool useScreenRipple = true;

    [Header("Phase Events / Hooks")]
    [Tooltip("Play a stinger / particles at the start of the pre-hold phase.")]
    public bool triggerStartEvents = true;

    [Tooltip("Play a stinger / particles at the freeze impact.")]
    public bool triggerImpactEvents = true;

    [Tooltip("Play a stinger / particles at the release/end.")]
    public bool triggerEndEvents = false;
}