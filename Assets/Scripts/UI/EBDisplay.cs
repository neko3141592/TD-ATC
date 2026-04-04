using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EBDisplay : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite ebOn;
    [SerializeField] private Sprite ebOff;
    [SerializeField] private TrainController train;
    [SerializeField] private bool autoFindTrain = true;

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

        public EBStateSample(float timestamp, bool value)
        {
            this.timestamp = timestamp;
            this.value = value;
        }
    }

    private void Reset()
    {
        targetImage = GetComponent<Image>();
    }

    private void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        ResetDelayState(GetRawEmergencyState());
    }

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

    private bool GetRawEmergencyState()
    {
        if (autoFindTrain && train == null)
        {
            train = FindFirstObjectByType<TrainController>();
        }

        return train != null && train.IsEmergencyBrakeActive;
    }

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

    private void ResetDelayState(bool rawState)
    {
        displayedEmergencyState = rawState;
        lastBufferedEmergencyState = rawState;
        hasBufferedState = true;
        stateReadBuffer.Clear();
    }
}
