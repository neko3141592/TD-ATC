using UnityEngine;
using UnityEngine.UI;

public class PatternApproachLampDisplay : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite lampOnSprite;
    [SerializeField] private Sprite lampOffSprite;
    [SerializeField] private ATCController atcController;
    [SerializeField] private bool autoFindAtcController = true;

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
    }

    private void Update()
    {
        if (targetImage == null)
        {
            return;
        }

        if (autoFindAtcController && atcController == null)
        {
            atcController = FindFirstObjectByType<ATCController>();
        }

        bool isApproaching = atcController != null && atcController.IsPatternApproaching;
        Sprite nextSprite = isApproaching ? lampOnSprite : lampOffSprite;
        if (nextSprite != null)
        {
            targetImage.sprite = nextSprite;
        }
    }
}
