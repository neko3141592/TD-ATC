using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TrainLongRailSound : MonoBehaviour
{
    [SerializeField] private TrainController train;
    [SerializeField] private AudioClip railLoopClip;
    [SerializeField, Min(0f)] private float fadeInStartSpeedKmH = 45f;
    [SerializeField, Min(0f)] private float fullVolumeSpeedKmH = 120f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float minVolume = 0.6f;
    [SerializeField, Min(0.01f)] private float minPitch = 0.85f;
    [SerializeField, Min(0.01f)] private float maxPitch = 1.25f;

    private AudioSource audioSource;

    private void Awake()
    {
        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
    }

    private void Update()
    {
        if (train == null || railLoopClip == null)
        {
            StopWind();
            return;
        }

        float speedKmH = Mathf.Max(0f, train.SpeedKmH);
        float speed01 = Mathf.InverseLerp(fadeInStartSpeedKmH, fullVolumeSpeedKmH, speedKmH);
        float targetVolume = Mathf.Lerp(minVolume, maxVolume, speed01);

        if (targetVolume <= 0.001f)
        {
            StopWind();
            return;
        }

        if (audioSource.clip != railLoopClip)
        {
            audioSource.clip = railLoopClip;
        }

        audioSource.volume = targetVolume;
        audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, speed01);

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private void OnDisable()
    {
        StopWind();
    }

    private void StopWind()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
