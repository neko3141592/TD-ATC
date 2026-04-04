using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NeutralDisplay : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite neutralOn;
    [SerializeField] private Sprite neutralOff;
    [SerializeField] private TrainController train;
    [SerializeField] private bool autoFindTrain = true;

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

        public NeutralStateSample(float timestamp, bool value)
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

        ResetDelayState(GetRawNeutralState());
    }

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

    private bool GetRawNeutralState()
    {
        if (autoFindTrain && train == null)
        {
            train = FindFirstObjectByType<TrainController>();
        }

        if (train == null)
        {
            return false;
        }

        return train.PowerNotch <= 0 && train.BrakeNotch <= 0;
    }

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

    private void ResetDelayState(bool rawState)
    {
        displayedNeutralState = rawState;
        lastBufferedNeutralState = rawState;
        hasBufferedState = true;
        stateReadBuffer.Clear();
    }
}
