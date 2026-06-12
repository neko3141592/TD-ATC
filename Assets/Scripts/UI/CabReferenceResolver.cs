using UnityEngine;
using UnityEngine.UI;

public static class CabReferenceResolver
{
    public static TrainController ResolveTrain(Component source, TrainController current)
    {
        if (current != null || source == null)
        {
            return current;
        }

        return source.GetComponentInParent<TrainController>();
    }

    public static T ResolveTrainComponent<T>(Component source, TrainController train, T current) where T : Component
    {
        if (current != null || source == null)
        {
            return current;
        }

        TrainController resolvedTrain = ResolveTrain(source, train);
        if (resolvedTrain != null)
        {
            T component = resolvedTrain.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            component = resolvedTrain.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return source.GetComponentInParent<T>();
    }
}

internal static class UIShadowUtility
{
    public static void ApplyShadow(Graphic graphic, bool enabled, Color color, Vector2 distance, bool useGraphicAlpha)
    {
        if (graphic == null)
        {
            return;
        }

        Shadow shadow = graphic.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = graphic.gameObject.AddComponent<Shadow>();
        }

        shadow.enabled = enabled;
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = useGraphicAlpha;
    }
}
