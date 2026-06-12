using UnityEngine;
using UnityEngine.UI;

public class ATCSignal : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image targetImage;

    [Header("Signal Sprites")]
    [SerializeField] private Sprite redSprite;
    [SerializeField] private Sprite greenSprite;
    [SerializeField] private Sprite signalOffSprite;

    [Header("ATC")]
    [SerializeField] private ATCController atc;

    /// <summary>
    /// 役割: Reset の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Reset()
    {
        targetImage = GetComponent<Image>();
    }

    /// <summary>
    /// 役割: Update の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        atc = CabReferenceResolver.ResolveTrainComponent(this, null, atc);

        if (targetImage == null || atc == null) return;

        if (atc.IsAtcCutOutActive && signalOffSprite != null)
        {
            targetImage.sprite = signalOffSprite;
            return;
        }

        targetImage.sprite = atc.IsNextBlockOccupied ? redSprite : greenSprite;
    }
}
