using UnityEngine;

public class ATCIndicatorDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ATCController atcController;

    [Header("Indicators")]
    [SerializeField] private IndicatorObjectPair emergencyOperation;
    [SerializeField] private IndicatorObjectPair atc;
    [SerializeField] private IndicatorObjectPair digitalAtc;
    [SerializeField] private IndicatorObjectPair atcServiceBrake;
    [SerializeField] private IndicatorObjectPair atcEmergencyBrake;

    [Header("Display Delay")]
    [SerializeField, Min(0f)] private float updateLagSeconds = 0f;

    private float nextReadTime = 0f;
    private bool displayedEmergencyBrakeOn = false;
    private bool displayedServiceBrakeOn = false;

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
    /// 役割: 起動時に参照を補完し、ATC表示灯の初期状態を反映します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Awake()
    {
        ResolveReferences();
        ResetDisplayedState();
        ApplyIndicators();
    }

    /// <summary>
    /// 役割: 毎フレーム、ATC状態に応じて表示灯を更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        ResolveReferences();
        RefreshIndicatorsWithDelay();
    }

    /// <summary>
    /// 役割: 未設定の ATCController 参照を親階層から補完します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResolveReferences()
    {
        if (atcController == null)
        {
            atcController = GetComponentInParent<ATCController>();
        }
    }

    /// <summary>
    /// 役割: 表示遅延を反映しながら、ATCブレーキ表示灯の表示状態を更新します。
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
    /// 役割: ATCController から現在の常用・非常ブレーキ状態を読み取ります。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ReadCurrentBrakeState()
    {
        displayedEmergencyBrakeOn = atcController != null && atcController.IsAtcEmergencyBrakeActive;
        displayedServiceBrakeOn = atcController != null && atcController.IsAtcServiceBrakeActive;
    }

    /// <summary>
    /// 役割: 保存済みの表示状態を GameObject のON/OFFへ反映します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyIndicators()
    {
        emergencyOperation.SetLit(false);
        atc.SetLit(false);
        digitalAtc.SetLit(true);

        atcServiceBrake.SetLit(displayedServiceBrakeOn || displayedEmergencyBrakeOn);
        atcEmergencyBrake.SetLit(displayedEmergencyBrakeOn);
    }

    /// <summary>
    /// 役割: 起動時やインスペクター変更時に、表示状態を現在のATC状態へ即時同期します。
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
