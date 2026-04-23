using UnityEngine;
using UnityEngine.UI;

public class PatternApproachLampDisplay : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite lampOnSprite;
    [SerializeField] private Sprite lampOffSprite;
    [SerializeField] private ATCController atcController;

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
    }

    /// <summary>
    /// 役割: Update の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        if (targetImage == null || atcController == null)
        {
            return;
        }

        bool isApproaching = atcController.IsPatternApproaching;
        Sprite nextSprite = isApproaching ? lampOnSprite : lampOffSprite;
        if (nextSprite != null)
        {
            targetImage.sprite = nextSprite;
        }
    }
}
