using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TrainWindSound : MonoBehaviour
{
    [SerializeField] private TrainController train;
    [SerializeField] private AudioClip windLoopClip;
    [SerializeField, Min(0f)] private float fadeInStartSpeedKmH = 45f;
    [SerializeField, Min(0f)] private float fullVolumeSpeedKmH = 120f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.6f;
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
        if (train == null || windLoopClip == null)
        {
            StopWind();
            return;
        }

        float speedKmH = Mathf.Max(0f, train.SpeedKmH);
        float speed01 = Mathf.InverseLerp(fadeInStartSpeedKmH, fullVolumeSpeedKmH, speedKmH);
        float targetVolume = speed01 * maxVolume;

        if (targetVolume <= 0.001f)
        {
            StopWind();
            return;
        }

        if (audioSource.clip != windLoopClip)
        {
            audioSource.clip = windLoopClip;
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
