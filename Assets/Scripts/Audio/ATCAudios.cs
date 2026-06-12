using UnityEngine;

/// <summary>
/// ATC解放時の警報音を管理するコンポーネントです。
/// ATC解放中は指定された AudioSource をループ再生し、解除されたら即停止します。
/// </summary>
public class ATCAudios : MonoBehaviour
{
    [Header("ref")]
    [SerializeField] private ATCController atcController;

    [Header("ATC Release/Warning Settings")]
    [Tooltip("ATC解放警報を再生するAudioSource。再生したい警報クリップを設定してください。")]
    [SerializeField] private AudioSource atcReleaseAudioSource;

    private bool wasAtcCutOutActive;

    private void Awake()
    {
        if (atcReleaseAudioSource == null)
        {
            atcReleaseAudioSource = GetComponent<AudioSource>();
        }

        if (atcReleaseAudioSource != null)
        {
            atcReleaseAudioSource.loop = true;
            atcReleaseAudioSource.playOnAwake = false;
            atcReleaseAudioSource.Stop();
        }
    }

    private void Update()
    {
        bool isAtcCutOutActive =
            atcController != null &&
            (atcController.IsAtcCutOutActive || atcController.IsEmergencyOperationActive);

        UpdateAtcReleaseSound(isAtcCutOutActive);
    }

    private void UpdateAtcReleaseSound(bool isAtcCutOutActive)
    {
        if (atcReleaseAudioSource == null)
        {
            return;
        }

        if (isAtcCutOutActive && !wasAtcCutOutActive)
        {
            atcReleaseAudioSource.loop = true;
            atcReleaseAudioSource.Play();
        }
        else if (!isAtcCutOutActive && wasAtcCutOutActive)
        {
            atcReleaseAudioSource.Stop();
        }

        wasAtcCutOutActive = isAtcCutOutActive;
    }
}