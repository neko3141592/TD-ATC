using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NeutralDisplay : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite neutralOn;
    [SerializeField] private Sprite neutralOff;
    [SerializeField] private TrainController train;

    [Header("Read Delay")]
    [SerializeField] private bool enableReadDelay = true;
    [SerializeField, Min(0f)] private float readDelaySeconds = 0.08f;

    private readonly Queue<NeutralStateSample> stateReadBuffer = new Queue<NeutralStateSample>();
    private bool displayedNeutralState;
    private bool hasBufferedState;
    private bool lastBufferedNeutralState;

    private struct NeutralStateSample
    {
        public float timestamp;
        public bool value;

        /// <summary>
        /// 役割: NeutralStateSample の処理を実行します。
        /// </summary>
        /// <param name="timestamp">timestamp を指定します。</param>
        /// <param name="value">value を指定します。</param>
        /// <returns>処理結果を返します。</returns>
        public NeutralStateSample(float timestamp, bool value)
        {
            this.timestamp = timestamp;
            this.value = value;
        }
    }

    /// <summary>
    /// 役割: Reset の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Reset()
    {
        targetImage = GetComponent<Image>();
    }

    /// <summary>
    /// 役割: Awake の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        ResetDelayState(GetRawNeutralState());
    }

    /// <summary>
    /// 役割: Update の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        if (targetImage == null)
        {
            return;
        }

        bool rawNeutralState = GetRawNeutralState();
        bool displayNeutralState = ResolveDelayedNeutralState(rawNeutralState);
        Sprite nextSprite = displayNeutralState ? neutralOn : neutralOff;

        if (nextSprite != null)
        {
            targetImage.sprite = nextSprite;
        }
    }

    /// <summary>
    /// 役割: GetRawNeutralState の処理を実行します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
    private bool GetRawNeutralState()
    {
        if (train == null)
        {
            return false;
        }

        return train.PowerNotch <= 0 && train.BrakeNotch <= 0;
    }

    /// <summary>
    /// 役割: ResolveDelayedNeutralState の処理を実行します。
    /// </summary>
    /// <param name="rawState">rawState を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private bool ResolveDelayedNeutralState(bool rawState)
    {
        if (!Application.isPlaying || !enableReadDelay || readDelaySeconds <= 0f)
        {
            ResetDelayState(rawState);
            return displayedNeutralState;
        }

        float now = Time.unscaledTime;
        if (!hasBufferedState || stateReadBuffer.Count == 0 || rawState != lastBufferedNeutralState)
        {
            stateReadBuffer.Enqueue(new NeutralStateSample(now, rawState));
            lastBufferedNeutralState = rawState;
            hasBufferedState = true;
        }

        float readableTime = now - readDelaySeconds;
        while (stateReadBuffer.Count > 0 && stateReadBuffer.Peek().timestamp <= readableTime)
        {
            displayedNeutralState = stateReadBuffer.Dequeue().value;
        }

        return displayedNeutralState;
    }

    /// <summary>
    /// 役割: ResetDelayState の処理を実行します。
    /// </summary>
    /// <param name="rawState">rawState を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ResetDelayState(bool rawState)
    {
        displayedNeutralState = rawState;
        lastBufferedNeutralState = rawState;
        hasBufferedState = true;
        stateReadBuffer.Clear();
    }
}
