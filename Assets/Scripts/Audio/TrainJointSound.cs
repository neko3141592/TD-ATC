using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TrainJointSound : MonoBehaviour
{
    [SerializeField] private TrainController train;
    [SerializeField] private AudioClip jointClip;
    [SerializeField, Min(0.1f)] private float jointIntervalM = 25f;
    [SerializeField, Min(0f)] private float minSpeedKmH = 3f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [SerializeField, Min(0.01f)] private float minPitch = 0.9f;
    [SerializeField, Min(0.01f)] private float maxPitch = 1.15f;
    [SerializeField, Min(0f)] private float maxPitchSpeedKmH = 120f;

    private AudioSource audioSource;
    private float nextJointDistanceM;

    private void Awake()
    {
        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void OnEnable()
    {
        ResetNextJointDistance();
    }

    private void Update()
    {
        if (train == null || audioSource == null || jointClip == null)
        {
            return;
        }

        float speedKmH = Mathf.Max(0f, train.SpeedKmH);
        if (speedKmH < minSpeedKmH)
        {
            ResetNextJointDistance();
            return;
        }

        float currentDistanceM = Mathf.Max(0f, train.DistanceM);
        if (currentDistanceM < nextJointDistanceM)
        {
            return;
        }

        float speed01 = Mathf.InverseLerp(minSpeedKmH, maxPitchSpeedKmH, speedKmH);
        audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, speed01);
        audioSource.PlayOneShot(jointClip, volume);

        float interval = Mathf.Max(0.1f, jointIntervalM);
        nextJointDistanceM += interval;
        while (nextJointDistanceM <= currentDistanceM)
        {
            nextJointDistanceM += interval;
        }
    }

    private void ResetNextJointDistance()
    {
        float currentDistanceM = train != null ? Mathf.Max(0f, train.DistanceM) : 0f;
        float interval = Mathf.Max(0.1f, jointIntervalM);
        nextJointDistanceM = (Mathf.Floor(currentDistanceM / interval) + 1f) * interval;
    }
}
