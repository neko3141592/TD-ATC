using UnityEngine;

public class Compressor : MonoBehaviour
{
    [SerializeField] private AudioSource oneShotSource;
    [SerializeField] private AudioSource loopSource;

    [SerializeField] private AudioClip startClip;
    [SerializeField] private AudioClip loopClip;
    [SerializeField] private AudioClip stopClip;

    private bool isRunning;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
        }

        if (loopSource == null)
        {
            loopSource = gameObject.AddComponent<AudioSource>();
        }

        oneShotSource.playOnAwake = false;

        loopSource.playOnAwake = false;
        loopSource.loop = true;
        loopSource.clip = loopClip;
    }

    public void StartCompressor()
    {
        if (isRunning)
        {
            return;
        }

        isRunning = true;

        if (startClip != null)
        {
            oneShotSource.PlayOneShot(startClip);
            Invoke(nameof(StartLoop), startClip.length);
        }
        else
        {
            StartLoop();
        }
    }

    public void StopCompressor()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;
        CancelInvoke(nameof(StartLoop));

        if (loopSource.isPlaying)
        {
            loopSource.Stop();
        }

        if (stopClip != null)
        {
            oneShotSource.Stop();
            oneShotSource.PlayOneShot(stopClip);
        }
    }

    public void ToggleCompressor()
    {
        if (isRunning)
        {
            StopCompressor();
        }
        else
        {
            StartCompressor();
        }
    }

    private void StartLoop()
    {
        if (!isRunning || loopClip == null)
        {
            return;
        }

        loopSource.clip = loopClip;
        loopSource.Play();
    }
}
