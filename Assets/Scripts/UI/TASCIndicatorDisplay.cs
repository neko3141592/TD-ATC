using UnityEngine;

public class TASCIndicatorDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TASCController tascController;

    [Header("Indicators")]
    [SerializeField] private IndicatorSpritePair tascPower;
    [SerializeField] private IndicatorSpritePair tascOperation;
    [SerializeField] private IndicatorSpritePair tascBrake;

    [System.Serializable]
    private struct IndicatorSpritePair
    {
        [SerializeField] private GameObject offObject;
        [SerializeField] private GameObject onObject;

        /// <summary>
        /// 役割: ランプのON/OFFに応じて、消灯スプライトと点灯スプライトを切り替えます。
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
    /// 役割: 起動時に参照を補完し、表示灯の初期状態を反映します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Awake()
    {
        ResolveReferences();
        RefreshIndicators();
    }

    /// <summary>
    /// 役割: 毎フレーム、TASC状態に応じて表示灯を更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        ResolveReferences();
        RefreshIndicators();
    }

    /// <summary>
    /// 役割: 未設定の TASCController 参照を親階層から補完します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResolveReferences()
    {
        if (tascController == null)
        {
            tascController = GetComponentInParent<TASCController>();
        }
    }

    /// <summary>
    /// 役割: TASC電源・TASC制御・TASCブレーキ表示灯の点灯状態を反映します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void RefreshIndicators()
    {
        tascPower.SetLit(true);

        bool isOperationOn = tascController != null && tascController.IsTascActive;
        bool isBrakeOn = tascController != null && tascController.CurrentTascBrakeStep > 0;

        tascOperation.SetLit(isOperationOn);
        tascBrake.SetLit(isBrakeOn);
    }

    /// <summary>
    /// 役割: インスペクター変更時に参照を補完し、表示灯へ即時反映します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnValidate()
    {
        ResolveReferences();
        RefreshIndicators();
    }
}
