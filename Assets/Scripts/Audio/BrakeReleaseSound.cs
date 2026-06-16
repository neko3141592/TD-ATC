using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(AudioSource))]
public class BrakeReleaseSound : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private AudioSource audioSource;

    [Header("Clips")]
    [FormerlySerializedAs("emergencyToServiceClip")]
    [SerializeField] private AudioClip emergencyToServiceReleaseClip;
    [SerializeField] private AudioClip smallReleaseClip;
    [SerializeField] private AudioClip mediumReleaseClip;
    [SerializeField] private AudioClip largeReleaseClip;
    [SerializeField] private AudioClip extraLargeReleaseClip;

    [Header("Target BC Detection")]
    [FormerlySerializedAs("minReleaseDropKPa")]
    [SerializeField, Min(0f)] private float minTargetDropKPa = 4f;
    [SerializeField, Min(0f)] private float mediumTargetDropKPa = 40f;
    [FormerlySerializedAs("largeReleaseDropKPa")]
    [SerializeField, Min(0f)] private float largeTargetDropKPa = 100f;
    [SerializeField, Min(0f)] private float extraLargeTargetDropKPa = 180f;
    [FormerlySerializedAs("classifyWindowSec")]
    [SerializeField, Min(0.01f)] private float targetDropWindowSec = 0.12f;

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float emergencyToServiceReleaseVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float smallReleaseVolume = 0.75f;
    [SerializeField, Range(0f, 1f)] private float mediumReleaseVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float largeReleaseVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float extraLargeReleaseVolume = 1f;

    [Header("Lock Time")]
    [SerializeField, Min(0f)] private float emergencyToServiceLockSec = 0f;
    [SerializeField, Min(0f)] private float smallReleaseLockSec = 0f;
    [SerializeField, Min(0f)] private float mediumReleaseLockSec = 0f;
    [SerializeField, Min(0f)] private float largeReleaseLockSec = 0f;
    [SerializeField, Min(0f)] private float extraLargeReleaseLockSec = 0f;

    private int previousManualBrakeNotch;
    private float previousTargetBCPressureKPa;
    private float targetDropWindowStartTime;
    private float accumulatedTargetDropKPa;
    private float releaseSoundLockedUntil;
    private bool isTargetDropWindowActive;

    private enum ReleaseLevel
    {
        Small,
        Medium,
        Large,
        ExtraLarge
    }

    private void Awake()
    {
        if (train == null) train = GetComponentInParent<TrainController>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void OnEnable()
    {
        previousManualBrakeNotch = train != null ? train.ManualBrakeNotch : 0;
        previousTargetBCPressureKPa = GetCurrentTargetBCPressureKPa();
        ResetTargetDropWindow();
    }

    private void LateUpdate()
    {
        if (train == null || audioSource == null)
        {
            return;
        }

        float currentTargetBCPressureKPa = GetCurrentTargetBCPressureKPa();
        int currentManualBrakeNotch = train.ManualBrakeNotch;
        bool movedEmergencyToService = HasMovedEmergencyToService(currentManualBrakeNotch);

        if (movedEmergencyToService)
        {
            TryPlayReleaseSound(emergencyToServiceReleaseClip, emergencyToServiceReleaseVolume, emergencyToServiceLockSec);
            ResetTargetDropWindow();
            previousManualBrakeNotch = currentManualBrakeNotch;
            previousTargetBCPressureKPa = currentTargetBCPressureKPa;
            return;
        }

        if (IsReleaseSoundLocked())
        {
            ResetTargetDropWindow();
            previousManualBrakeNotch = currentManualBrakeNotch;
            previousTargetBCPressureKPa = currentTargetBCPressureKPa;
            return;
        }

        float targetDropKPa = previousTargetBCPressureKPa - currentTargetBCPressureKPa;
        if (targetDropKPa > 0f && (isTargetDropWindowActive || targetDropKPa >= minTargetDropKPa))
        {
            UpdateTargetDropWindow(targetDropKPa);
        }
        else if (isTargetDropWindowActive && Time.time - targetDropWindowStartTime >= targetDropWindowSec)
        {
            CompleteTargetDropWindow();
        }

        previousManualBrakeNotch = currentManualBrakeNotch;
        previousTargetBCPressureKPa = currentTargetBCPressureKPa;
    }

    private float GetCurrentTargetBCPressureKPa()
    {
        return train != null ? Mathf.Max(0f, train.CurrentTargetBCPressureKPa) : 0f;
    }

    private bool HasMovedEmergencyToService(int currentManualBrakeNotch)
    {
        int emergencyBrakeNotch = Mathf.Max(1, train.EmergencyBrakeNotch);
        return previousManualBrakeNotch >= emergencyBrakeNotch &&
            currentManualBrakeNotch > 0 &&
            currentManualBrakeNotch < emergencyBrakeNotch;
    }

    private void UpdateTargetDropWindow(float targetDropKPa)
    {
        if (!isTargetDropWindowActive)
        {
            isTargetDropWindowActive = true;
            targetDropWindowStartTime = Time.time;
            accumulatedTargetDropKPa = 0f;
        }

        accumulatedTargetDropKPa += Mathf.Max(0f, targetDropKPa);

        bool exceededHighestThreshold = accumulatedTargetDropKPa >= extraLargeTargetDropKPa;
        bool windowElapsed = Time.time - targetDropWindowStartTime >= targetDropWindowSec;
        if (exceededHighestThreshold || windowElapsed)
        {
            CompleteTargetDropWindow();
        }
    }

    private void CompleteTargetDropWindow()
    {
        PlayReleaseClip(accumulatedTargetDropKPa);
        ResetTargetDropWindow();
    }

    private void PlayReleaseClip(float releaseDropKPa)
    {
        if (releaseDropKPa < minTargetDropKPa)
        {
            return;
        }

        ReleaseLevel level = GetReleaseLevel(releaseDropKPa);
        if (!TryGetReleasePlayback(level, out AudioClip clip, out float volume, out float lockSec))
        {
            return;
        }

        TryPlayReleaseSound(clip, volume, lockSec);
    }

    private ReleaseLevel GetReleaseLevel(float releaseDropKPa)
    {
        if (releaseDropKPa >= extraLargeTargetDropKPa)
        {
            return ReleaseLevel.ExtraLarge;
        }

        if (releaseDropKPa >= largeTargetDropKPa)
        {
            return ReleaseLevel.Large;
        }

        if (releaseDropKPa >= mediumTargetDropKPa)
        {
            return ReleaseLevel.Medium;
        }

        return ReleaseLevel.Small;
    }

    private bool TryGetReleasePlayback(ReleaseLevel level, out AudioClip clip, out float volume, out float lockSec)
    {
        clip = GetReleaseClip(level);
        if (clip == null)
        {
            clip = GetFallbackReleaseClip(level);
        }

        volume = GetReleaseVolume(level);
        lockSec = GetReleaseLockSec(level);
        return clip != null;
    }

    private AudioClip GetReleaseClip(ReleaseLevel level)
    {
        switch (level)
        {
            case ReleaseLevel.ExtraLarge:
                return extraLargeReleaseClip;
            case ReleaseLevel.Large:
                return largeReleaseClip;
            case ReleaseLevel.Medium:
                return mediumReleaseClip;
            default:
                return smallReleaseClip;
        }
    }

    private AudioClip GetFallbackReleaseClip(ReleaseLevel level)
    {
        switch (level)
        {
            case ReleaseLevel.ExtraLarge:
                return largeReleaseClip != null ? largeReleaseClip :
                    mediumReleaseClip != null ? mediumReleaseClip : smallReleaseClip;
            case ReleaseLevel.Large:
                return mediumReleaseClip != null ? mediumReleaseClip :
                    extraLargeReleaseClip != null ? extraLargeReleaseClip : smallReleaseClip;
            case ReleaseLevel.Medium:
                return smallReleaseClip != null ? smallReleaseClip :
                    largeReleaseClip != null ? largeReleaseClip : extraLargeReleaseClip;
            default:
                return mediumReleaseClip != null ? mediumReleaseClip :
                    largeReleaseClip != null ? largeReleaseClip : extraLargeReleaseClip;
        }
    }

    private float GetReleaseVolume(ReleaseLevel level)
    {
        switch (level)
        {
            case ReleaseLevel.ExtraLarge:
                return extraLargeReleaseVolume;
            case ReleaseLevel.Large:
                return largeReleaseVolume;
            case ReleaseLevel.Medium:
                return mediumReleaseVolume;
            default:
                return smallReleaseVolume;
        }
    }

    private float GetReleaseLockSec(ReleaseLevel level)
    {
        switch (level)
        {
            case ReleaseLevel.ExtraLarge:
                return extraLargeReleaseLockSec;
            case ReleaseLevel.Large:
                return largeReleaseLockSec;
            case ReleaseLevel.Medium:
                return mediumReleaseLockSec;
            default:
                return smallReleaseLockSec;
        }
    }

    private bool TryPlayReleaseSound(AudioClip clip, float volume, float lockSec)
    {
        if (clip == null || IsReleaseSoundLocked())
        {
            return false;
        }

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        releaseSoundLockedUntil = Time.time + Mathf.Max(0f, lockSec);
        return true;
    }

    private bool IsReleaseSoundLocked()
    {
        return Time.time < releaseSoundLockedUntil;
    }

    private void ResetTargetDropWindow()
    {
        isTargetDropWindowActive = false;
        targetDropWindowStartTime = 0f;
        accumulatedTargetDropKPa = 0f;
    }
}
