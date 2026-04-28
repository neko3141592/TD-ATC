using UnityEngine;

public class TrainBrakeIndicatorDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;

    [Header("Indicators")]
    [SerializeField] private IndicatorObjectPair rollingPrevent;
    [SerializeField] private IndicatorObjectPair keep;
    [SerializeField] private IndicatorObjectPair regenReleased;

    [Header("Display Delay")]
    [SerializeField, Min(0f)] private float updateLagSeconds = 0f;

    private float nextReadTime = 0f;
    private bool displayedRollingPreventOn = false;

    [System.Serializable]
    private struct IndicatorObjectPair
    {
        [SerializeField] private GameObject offObject;
        [SerializeField] private GameObject onObject;

        /// <summary>
        /// 役割: 表示灯の状態に合わせて、消灯用 GameObject と点灯用 GameObject を切り替えます。
        /// </summary>
        /// <param name="isOn">点灯させる場合は true、消灯させる場合は false を指定します。</param>
        /// <remarks>返り値はありません。</remarks>
        public void SetLit(bool isOn)
        {
            if (offObject != null)
            {
                offObject.SetActive(!isOn);
            }

            if (onObject != null)
            {
                onObject.SetActive(isOn);
            }
        }
    }

    /// <summary>
    /// 役割: 起動時に参照を補完し、ブレーキ表示灯の初期状態を反映します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Awake()
    {
        ResolveReferences();
        ResetDisplayedState();
        ApplyIndicators();
    }

    /// <summary>
    /// 役割: 毎フレーム、列車のブレーキ状態に応じて表示灯を更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        ResolveReferences();
        RefreshIndicatorsWithDelay();
    }

    /// <summary>
    /// 役割: 未設定の TrainController 参照を親階層から補完します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResolveReferences()
    {
        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }
    }

    /// <summary>
    /// 役割: 表示遅延を反映しながら、ブレーキ表示灯の表示状態を更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void RefreshIndicatorsWithDelay()
    {
        if (updateLagSeconds <= 0f || Time.time >= nextReadTime)
        {
            ReadCurrentBrakeState();
            nextReadTime = Time.time + Mathf.Max(0f, updateLagSeconds);
        }

        ApplyIndicators();
    }

    /// <summary>
    /// 役割: TrainController から表示灯に使うブレーキ状態を読み取ります。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ReadCurrentBrakeState()
    {
        displayedRollingPreventOn = train != null && train.IsRollingPreventionActive;
    }

    /// <summary>
    /// 役割: 保存済みの表示状態を GameObject のON/OFFへ反映します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyIndicators()
    {
        rollingPrevent.SetLit(displayedRollingPreventOn);
        keep.SetLit(false);
        regenReleased.SetLit(false);
    }

    /// <summary>
    /// 役割: 起動時やインスペクター変更時に、表示状態を現在の列車状態へ即時同期します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResetDisplayedState()
    {
        ReadCurrentBrakeState();
        nextReadTime = Time.time + Mathf.Max(0f, updateLagSeconds);
    }

    /// <summary>
    /// 役割: インスペクター変更時に参照を補完し、表示灯へ即時反映します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnValidate()
    {
        ResolveReferences();
        ResetDisplayedState();
        ApplyIndicators();
    }
}
