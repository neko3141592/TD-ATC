using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CarBuzzer : MonoBehaviour
{
    private AudioSource audioSource;

    /// <summary>
    /// 役割: Awake の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
    }

    /// <summary>
    /// 役割: Update の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        if (Input.GetKey(KeyCode.B))
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }
}
