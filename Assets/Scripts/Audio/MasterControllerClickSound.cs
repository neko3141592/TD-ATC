using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MasterControllerClickSound : MonoBehaviour
{
    [SerializeField] private TrainController train;
    [SerializeField] private AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Min(0.01f)] private float minPitch = 0.95f;
    [SerializeField, Min(0.01f)] private float maxPitch = 1.05f;

    private AudioSource audioSource;
    private int previousPowerNotch;
    private int previousBrakeNotch;

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
        previousPowerNotch = train != null ? train.ManualPowerNotch : 0;
        previousBrakeNotch = train != null ? train.ManualBrakeNotch : 0;
    }

    private void LateUpdate()
    {
        if (train == null || audioSource == null || clickClip == null)
        {
            return;
        }

        int currentPowerNotch = train.ManualPowerNotch;
        int currentBrakeNotch = train.ManualBrakeNotch;
        if (currentPowerNotch != previousPowerNotch || currentBrakeNotch != previousBrakeNotch)
        {
            PlayClick();
        }

        previousPowerNotch = currentPowerNotch;
        previousBrakeNotch = currentBrakeNotch;
    }

    private void PlayClick()
    {
        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(clickClip, volume);
    }
}
