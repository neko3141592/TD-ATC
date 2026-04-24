using UnityEngine;

public class Compressor : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource oneShotSource;
    [SerializeField] private AudioSource loopSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip startClip;
    [SerializeField] private AudioClip loopClip;
    [SerializeField] private AudioClip stopClip;

    private bool isRunning;

    public bool IsRunning => isRunning;

    /// <summary>
    /// 役割: Awake の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: StartCompressor の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: StopCompressor の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: ToggleCompressor の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: StartLoop の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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
