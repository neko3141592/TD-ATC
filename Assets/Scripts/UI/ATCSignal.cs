using UnityEngine;
using UnityEngine.UI;

public class ATCSignal : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite redSprite;
    [SerializeField] private Sprite greenSprite;
    [SerializeField] private ATCController atc;

    private void Reset()
    {
        targetImage = GetComponent<Image>();
    }

    private void Update()
    {
        if (targetImage == null || atc == null) return;

        // ATC側で判定したboolを使う
        targetImage.sprite = greenSprite;
    }
}
