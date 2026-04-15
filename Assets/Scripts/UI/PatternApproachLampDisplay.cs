using UnityEngine;
using UnityEngine.UI;

public class PatternApproachLampDisplay : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite lampOnSprite;
    [SerializeField] private Sprite lampOffSprite;
    [SerializeField] private ATCController atcController;

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
