using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EBDisplay : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite ebOn;
    [SerializeField] private Sprite ebOff;
    [SerializeField] private TrainController train;

    [Header("Read Delay")]
    [SerializeField] private bool enableReadDelay = true;
    [SerializeField, Min(0f)] private float readDelaySeconds = 0.08f;

    private readonly Queue<EBStateSample> stateReadBuffer = new Queue<EBStateSample>();
    private bool displayedEmergencyState;
    private bool hasBufferedState;
    private bool lastBufferedEmergencyState;

    private struct EBStateSample
    {
        public float timestamp;
        public bool value;

        /// <summary>
        /// 役割: EBStateSample の処理を実行します。
        /// </summary>
        /// <param name="timestamp">timestamp を指定します。</param>
        /// <param name="value">value を指定します。</param>
        /// <returns>処理結果を返します。</returns>
        public EBStateSample(float timestamp, bool value)
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

        ResetDelayState(GetRawEmergencyState());
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

        bool rawEmergencyState = GetRawEmergencyState();
        bool displayEmergencyState = ResolveDelayedEmergencyState(rawEmergencyState);
        Sprite nextSprite = displayEmergencyState ? ebOn : ebOff;

        if (nextSprite != null)
        {
            targetImage.sprite = nextSprite;
        }
    }

    /// <summary>
    /// 役割: GetRawEmergencyState の処理を実行します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
    private bool GetRawEmergencyState()
    {
        return train != null && train.IsEmergencyBrakeActive;
    }

    /// <summary>
    /// 役割: ResolveDelayedEmergencyState の処理を実行します。
    /// </summary>
    /// <param name="rawState">rawState を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private bool ResolveDelayedEmergencyState(bool rawState)
    {
        if (!Application.isPlaying || !enableReadDelay || readDelaySeconds <= 0f)
        {
            ResetDelayState(rawState);
            return displayedEmergencyState;
        }

        float now = Time.unscaledTime;
        if (!hasBufferedState || stateReadBuffer.Count == 0 || rawState != lastBufferedEmergencyState)
        {
            stateReadBuffer.Enqueue(new EBStateSample(now, rawState));
            lastBufferedEmergencyState = rawState;
            hasBufferedState = true;
        }

        float readableTime = now - readDelaySeconds;
        while (stateReadBuffer.Count > 0 && stateReadBuffer.Peek().timestamp <= readableTime)
        {
            displayedEmergencyState = stateReadBuffer.Dequeue().value;
        }

        return displayedEmergencyState;
    }

    /// <summary>
    /// 役割: ResetDelayState の処理を実行します。
    /// </summary>
    /// <param name="rawState">rawState を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ResetDelayState(bool rawState)
    {
        displayedEmergencyState = rawState;
        lastBufferedEmergencyState = rawState;
        hasBufferedState = true;
        stateReadBuffer.Clear();
    }
}
