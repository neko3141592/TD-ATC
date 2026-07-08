using System;
using System.Collections.Generic;

public class TimsDataBus
{
    private readonly Dictionary<TimsTagKey, TimsValue> tags = new();

    public void SetBool(TimsTagKey key, bool value)
    {
        SetValue(key, TimsValue.FromBool(value));
    }

    public void SetInt(TimsTagKey key, int value)
    {
        SetValue(key, TimsValue.FromInt(value));
    }

    public void SetFloat(TimsTagKey key, float value)
    {
        SetValue(key, TimsValue.FromFloat(value));
    }

    public void SetString(TimsTagKey key, string value)
    {
        SetValue(key, TimsValue.FromString(value));
    }

    public void SetIntArray(TimsTagKey key, int[] value)
    {
        SetValue(key, TimsValue.FromIntArray(value));
    }

    public void SetFloatArray(TimsTagKey key, float[] value)
    {
        SetValue(key, TimsValue.FromFloatArray(value));
    }

    public void SetStringArray(TimsTagKey key, string[] value)
    {
        SetValue(key, TimsValue.FromStringArray(value));
    }

    public bool TryGetBool(TimsTagKey key, out bool value)
    {
        if (TryGetValue(key, TimsValueType.Bool, out TimsValue timsValue))
        {
            value = timsValue.BoolValue;
            return true;
        }

        value = false;
        return false;
    }

    public bool TryGetInt(TimsTagKey key, out int value)
    {
        if (TryGetValue(key, TimsValueType.Int, out TimsValue timsValue))
        {
            value = timsValue.IntValue;
            return true;
        }

        value = 0;
        return false;
    }

    public bool TryGetFloat(TimsTagKey key, out float value)
    {
        if (TryGetValue(key, TimsValueType.Float, out TimsValue timsValue))
        {
            value = timsValue.FloatValue;
            return true;
        }

        value = 0f;
        return false;
    }

    public bool TryGetString(TimsTagKey key, out string value)
    {
        if (TryGetValue(key, TimsValueType.String, out TimsValue timsValue))
        {
            value = timsValue.StringValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public bool TryGetIntArray(TimsTagKey key, out int[] value)
    {
        if (TryGetValue(key, TimsValueType.IntArray, out TimsValue timsValue))
        {
            value = timsValue.IntArrayValue;
            return true;
        }

        value = Array.Empty<int>();
        return false;
    }

    public bool TryGetFloatArray(TimsTagKey key, out float[] value)
    {
        if (TryGetValue(key, TimsValueType.FloatArray, out TimsValue timsValue))
        {
            value = timsValue.FloatArrayValue;
            return true;
        }

        value = Array.Empty<float>();
        return false;
    }

    public bool TryGetStringArray(TimsTagKey key, out string[] value)
    {
        if (TryGetValue(key, TimsValueType.StringArray, out TimsValue timsValue))
        {
            value = timsValue.StringArrayValue;
            return true;
        }

        value = Array.Empty<string>();
        return false;
    }

    public List<KeyValuePair<TimsTagKey, TimsValue>> GetSnapshot()
    {
        return new List<KeyValuePair<TimsTagKey, TimsValue>>(tags);
    }

    public bool Remove(TimsTagKey key)
    {
        return tags.Remove(key);
    }

    private void SetValue(TimsTagKey key, TimsValue value)
    {
        tags[key] = value;
    }

    private bool TryGetValue(TimsTagKey key, TimsValueType expectedType, out TimsValue value)
    {
        if (tags.TryGetValue(key, out value) && value.Type == expectedType)
        {
            return true;
        }

        value = default;
        return false;
    }
}
