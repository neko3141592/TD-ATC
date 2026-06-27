using UnityEngine;

public class EBAudio : MonoBehaviour
{
    [SerializeField] EBController ebController;
    [SerializeField] AudioSource ebAudio;
    void Awake()
    {
        ebController = GetComponentInParent<EBController>();
        ebAudio = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (ebAudio == null)
        {
            return;
        }

        if (ebController.IsActive)
        {
            if (!ebAudio.isPlaying)
            {
                ebAudio.Play();
            }
        }
        else
        {
            if (ebAudio.isPlaying)
            {
                ebAudio.Stop();
            }
        }

        
    }
}