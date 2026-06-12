using UnityEngine;

public class BrakeReleaseSound : MonoBehaviour
{
    [SerializeField] private TrainController train;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip emergencyToServiceClip;

    private int previousManualBrakeNotch;

    private void Awake()
    {
        if (train == null) train = GetComponentInParent<TrainController>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        previousManualBrakeNotch = train != null ? train.ManualBrakeNotch : 0;
    }

    private void LateUpdate()
    {
        if (train == null || audioSource == null || emergencyToServiceClip == null)
        {
            return;
        }

        int current = train.ManualBrakeNotch;
        int emergency = Mathf.Max(1, train.EmergencyBrakeNotch);

        bool movedEmergencyToService =
            previousManualBrakeNotch >= emergency &&
            current > 0 &&
            current < emergency;

        if (movedEmergencyToService)
        {
            audioSource.PlayOneShot(emergencyToServiceClip);
        }

        previousManualBrakeNotch = current;
    }
}