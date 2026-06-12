using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ProtectionRadioSound : MonoBehaviour
{
    [SerializeField] private KeyCode transmitKey = KeyCode.P;
    [SerializeField] private KeyCode releaseKey = KeyCode.O;
    [SerializeField] private AudioClip radioClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private AudioSource audioSource;
    private bool isTransmitting;

    public bool IsTransmitting => isTransmitting;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.volume = volume;
    }

    private void Update()
    {
        if (Input.GetKeyDown(transmitKey))
        {
            StartTransmit();
        }

        if (Input.GetKeyDown(releaseKey))
        {
            StopTransmit();
        }
    }

    public void StartTransmit()
    {
        if (isTransmitting || audioSource == null || radioClip == null)
        {
            return;
        }

        isTransmitting = true;
        audioSource.clip = radioClip;
        audioSource.loop = true;
        audioSource.volume = volume;
        audioSource.Play();
    }

    public void StopTransmit()
    {
        if (!isTransmitting)
        {
            return;
        }

        isTransmitting = false;
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    public void ToggleTransmit()
    {
        if (isTransmitting)
        {
            StopTransmit();
        }
        else
        {
            StartTransmit();
        }
    }
}
