using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class CabNotchTextDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private TMP_Text targetText;

    [Header("Labels")]
    [SerializeField] private string manualPrefix = "MNU";
    [SerializeField] private string tascPrefix = "TASC";
    [SerializeField] private string emergencyBrakeLabel = "EB";
    [SerializeField] private string neutralLabel = "OFF";

    [Header("Display Lag")]
    [SerializeField, Min(0f)] private float updateLagSeconds = 0.2f;

    private string lastText = string.Empty;
    private float nextRefreshTime = 0f;

    /// <summary>
    /// 役割: コンポーネント初期化時に TMP と列車関連参照を補完します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Awake()
    {
        ResolveReferences();
        RefreshText(true);
    }

    /// <summary>
    /// 役割: 毎フレーム、現在選択されているノッチ表示を更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        ResolveReferences();
        RefreshText(false);
    }

    /// <summary>
    /// 役割: 未設定の参照を同じ GameObject や親階層から探して補完します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResolveReferences()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }

        if (notchManager == null)
        {
            if (train != null)
            {
                notchManager = train.GetComponent<NotchManager>();
            }

            if (notchManager == null)
            {
                notchManager = GetComponentInParent<NotchManager>();
            }
        }
    }

    /// <summary>
    /// 役割: 表示文字列を作り、更新間隔を過ぎた時だけ最新値を TMP に反映します。
    /// </summary>
    /// <param name="force">文字列が同じでも強制反映する場合は true を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void RefreshText(bool force)
    {
        if (targetText == null)
        {
            return;
        }

        string nextText = BuildDisplayText();
        if (force || updateLagSeconds <= 0f)
        {
            ApplyText(nextText, force);
            nextRefreshTime = Time.time + updateLagSeconds;
            return;
        }

        if (nextText == lastText || Time.time < nextRefreshTime)
        {
            return;
        }

        ApplyText(nextText, false);
        nextRefreshTime = Time.time + updateLagSeconds;
    }

    /// <summary>
    /// 役割: TMP に文字列を反映します。
    /// </summary>
    /// <param name="nextText">表示する文字列を指定します。</param>
    /// <param name="force">文字列が同じでも強制反映する場合は true を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyText(string nextText, bool force)
    {
        if (!force && nextText == lastText)
        {
            return;
        }

        lastText = nextText;
        targetText.text = nextText;
    }

    /// <summary>
    /// 役割: 現在の制御元とノッチから運転台表示用文字列を作ります。
    /// </summary>
    /// <returns>運転台に表示するノッチ文字列を返します。</returns>
    private string BuildDisplayText()
    {
        if (notchManager != null && notchManager.IsTASCBrakeSelected && notchManager.TASCBrakeStep > 0)
        {
            return $"{tascPrefix}-B{notchManager.TASCBrakeStep}";
        }

        if (train == null)
        {
            return $"{manualPrefix}-B0";
        }

        if (train.PowerNotch > 0)
        {
            return $"{manualPrefix}-P{train.PowerNotch}";
        }

        if (train.BrakeNotch > 0)
        {
            if (train.IsEmergencyBrakeActive)
            {
                return $"{manualPrefix}-{emergencyBrakeLabel}";
            }

            return $"{manualPrefix}-B{train.BrakeNotch}";
        }

        return neutralLabel;
    }

    /// <summary>
    /// 役割: インスペクター変更時に参照を補完し、表示を即時更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnValidate()
    {
        ResolveReferences();
        RefreshText(true);
    }
}
