using System;
using EFT;
using TrueView.Configuration;
using TrueView.Core;
using TrueView.Utilities;
using UnityEngine;

namespace TrueView.Systems;

/// <summary>
/// Adrenaline dump. Being shot at (hits taken or nearby enemy gunfire)
/// raises an adrenaline level that decays
/// over several seconds. While elevated it feeds three things: a slight
/// hands tremor (read by SwaySystem), a tunnel-vision FOV pinch (read by
/// DynamicFovSystem), and a synthesized low-frequency heartbeat whose rate
/// and volume scale with the level. No audio assets needed — the lub-dub
/// clip is generated procedurally at first use.
/// </summary>
internal static class AdrenalineSystem
{
    private const float HeartSampleRate = 22050f;

    private static float _level;
    private static float _nextBeat;
    private static AudioClip _heartClip;
    private static AudioSource _heartSource;

    /// <summary>0..1 — how wound up the player currently is.</summary>
    public static float Level => _level;

    /// <summary>FOV multiplier contribution (tunnel vision under adrenaline).</summary>
    public static float FovFactor =>
        1f - _level * (HandlingConfig.AdrenalineFovPinch?.Value ?? 0f) * 0.01f;

    /// <summary>Extra hands-tremor amplitude contribution for SwaySystem.</summary>
    public static float TremorAdd =>
        _level * (HandlingConfig.AdrenalineTremor?.Value ?? 0f);

    /// <summary>Called for every shooter in the raid — spikes if near/self.</summary>
    public static void OnShot(Player shooter, Player localPlayer)
    {
        if (HandlingConfig.AdrenalineEnabled?.Value != true ||
            HandlingConfig.Enabled?.Value != true || localPlayer == null)
            return;

        float spike;
        if (shooter == localPlayer)
        {
            // Own shots alone don't trigger adrenaline — the player firing at
            // nothing isn't under stress. Once combat is already active
            // (level > 0 from being shot at or hit), each shot keeps it fed.
            if (_level <= 0f) return;
            spike = 0.1f;
        }
        else
        {
            float dist;
            try { dist = Vector3.Distance(shooter.Position, localPlayer.Position); }
            catch { return; }
            float radius = HandlingConfig.AdrenalineRadius?.Value ?? 40f;
            if (dist > radius) return;
            spike = Mathf.Lerp(0.45f, 0.12f, dist / radius);
        }
        _level = Mathf.Clamp01(_level + spike);
    }

    /// <summary>Called when the local player takes a hit — full spike.</summary>
    public static void OnHit()
    {
        if (HandlingConfig.AdrenalineEnabled?.Value == true)
            _level = 1f;
    }

    public static void Tick(Player player, float dt)
    {
        if (HandlingConfig.AdrenalineEnabled?.Value != true || HandlingConfig.Enabled?.Value != true)
        {
            if (_level > 0f) Reset();
            return;
        }

        float decay = Mathf.Max(1f, HandlingConfig.AdrenalineDecay?.Value ?? 8f);
        _level = Mathf.Max(0f, _level - dt / decay);
        if (_level < 0.005f) _level = 0f;

        UpdateHeartbeat(dt);
    }

    private static void UpdateHeartbeat(float dt)
    {
        float volume = (HandlingConfig.AdrenalineHeartbeat?.Value ?? 0f) * _level;
        if (volume < 0.02f)
        {
            if (_heartSource != null && _heartSource.isPlaying)
                _heartSource.Stop();
            _nextBeat = 0f;
            return;
        }

        if (_heartSource == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            _heartSource = cam.GetComponent<AudioSource>();
            if (_heartSource == null)
                _heartSource = cam.gameObject.AddComponent<AudioSource>();
            BreathingSystem.RouteToGameMixer(_heartSource);
            _heartSource.spatialBlend = 0f;
            _heartSource.loop = false;
            _heartSource.playOnAwake = false;
            _heartSource.ignoreListenerPause = true;
        }
        // Rebuilt if Unity dropped its data on an audio reconfigure.
        if (!BreathingSystem.ClipValid(_heartClip))
            _heartClip = BuildHeartbeatClip();

        _nextBeat -= dt;
        if (_nextBeat <= 0f && !_heartSource.isPlaying)
        {
            _heartSource.clip = _heartClip;
            // A heavy exhale covers the heart a little.
            _heartSource.volume = Mathf.Clamp01(volume * BreathingSystem.HeartMask);
            _heartSource.Play();
            // ~70bpm at full calm up to ~140bpm at peak adrenaline, a touch
            // quicker on each inhale (as a real heart does).
            float bpm = Mathf.Lerp(70f, 140f, _level) * BreathingSystem.HeartRateMod;
            _nextBeat = 60f / bpm;
        }
    }

    /// <summary>
    /// Synthesizes a single "lub-dub": two low-frequency decaying sine
    /// bursts ~150ms apart, with a soft exponential envelope.
    /// </summary>
    private static AudioClip BuildHeartbeatClip()
    {
        int len = (int)(HeartSampleRate * 0.55f);
        var samples = new float[len];
        Thump(samples, 0.00f, 55f, 0.14f, 1f);    // lub
        Thump(samples, 0.16f, 48f, 0.12f, 0.75f); // dub
        var clip = AudioClip.Create("adrenaline_heartbeat", len, 1, (int)HeartSampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static void Thump(float[] samples, float startSec, float freq, float duration, float amp)
    {
        int start = (int)(startSec * HeartSampleRate);
        int count = (int)(duration * HeartSampleRate);
        for (int i = 0; i < count && start + i < samples.Length; i++)
        {
            float t = i / HeartSampleRate;
            float env = Mathf.Pow(1f - t / duration, 2f);
            samples[start + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * env * amp;
        }
    }

    public static void Reset()
    {
        _level = 0f;
        _nextBeat = 0f;
        if (_heartSource != null && _heartSource.isPlaying)
            _heartSource.Stop();
    }
}
