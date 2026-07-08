using System;
using System.Linq;

public readonly struct TimsValue : IEquatable<TimsValue>
{
    public readonly TimsValueType Type;

    private readonly bool boolValue;
    private readonly int intValue;
    private readonly float floatValue;
    private readonly string stringValue;
    private readonly int[] intArrayValue;
    private readonly float[] floatArrayValue;
    private readonly string[] stringArrayValue;

    public bool BoolValue => boolValue;
    public int IntValue => intValue;
    public float FloatValue => floatValue;
    public string StringValue => stringValue;
    public int[] IntArrayValue => CloneIntArray();
    public float[] FloatArrayValue => CloneFloatArray();
    public string[] StringArrayValue => CloneStringArray();

    private TimsValue(
        TimsValueType type,
        bool boolValue,
        int intValue,
        float floatValue,
        string stringValue,
        int[] intArrayValue,
        float[] floatArrayValue,
        string[] stringArrayValue)
    {
        Type = type;
        this.boolValue = boolValue;
        this.intValue = intValue;
        this.floatValue = floatValue;
        this.stringValue = stringValue;
        this.intArrayValue = intArrayValue;
        this.floatArrayValue = floatArrayValue;
        this.stringArrayValue = stringArrayValue;
    }

    public static TimsValue FromBool(bool value)
    {
        return new TimsValue(TimsValueType.Bool, value, 0, 0f, string.Empty, null, null, null);
    }

    public static TimsValue FromInt(int value)
    {
        return new TimsValue(TimsValueType.Int, false, value, 0f, string.Empty, null, null, null);
    }

    public static TimsValue FromFloat(float value)
    {
        return new TimsValue(TimsValueType.Float, false, 0, value, string.Empty, null, null, null);
    }

    public static TimsValue FromString(string value)
    {
        return new TimsValue(TimsValueType.String, false, 0, 0f, value ?? string.Empty, null, null, null);
    }

    public static TimsValue FromIntArray(int[] value)
    {
        return new TimsValue(TimsValueType.IntArray, false, 0, 0f, string.Empty, Clone(value), null, null);
    }

    public static TimsValue FromFloatArray(float[] value)
    {
        return new TimsValue(TimsValueType.FloatArray, false, 0, 0f, string.Empty, null, Clone(value), null);
    }

    public static TimsValue FromStringArray(string[] value)
    {
        return new TimsValue(TimsValueType.StringArray, false, 0, 0f, string.Empty, null, null, Clone(value));
    }

    public bool Equals(TimsValue other)
    {
        if (Type != other.Type)
        {
            return false;
        }

        switch (Type)
        {
            case TimsValueType.Bool:
                return boolValue == other.boolValue;
            case TimsValueType.Int:
                return intValue == other.intValue;
            case TimsValueType.Float:
                return floatValue.Equals(other.floatValue);
            case TimsValueType.String:
                return stringValue == other.stringValue;
            case TimsValueType.IntArray:
                return intArrayValue.SequenceEqual(other.intArrayValue);
            case TimsValueType.FloatArray:
                return floatArrayValue.SequenceEqual(other.floatArrayValue);
            case TimsValueType.StringArray:
                return stringArrayValue.SequenceEqual(other.stringArrayValue);
            default:
                return false;
        }
    }

    public override bool Equals(object obj)
    {
        return obj is TimsValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Type;
            hash = (hash * 397) ^ boolValue.GetHashCode();
            hash = (hash * 397) ^ intValue;
            hash = (hash * 397) ^ floatValue.GetHashCode();
            hash = (hash * 397) ^ (stringValue != null ? stringValue.GetHashCode() : 0);

            foreach (int value in intArrayValue ?? Array.Empty<int>())
            {
                hash = (hash * 397) ^ value;
            }

            foreach (float value in floatArrayValue ?? Array.Empty<float>())
            {
                hash = (hash * 397) ^ value.GetHashCode();
            }

            foreach (string value in stringArrayValue ?? Array.Empty<string>())
            {
                hash = (hash * 397) ^ (value != null ? value.GetHashCode() : 0);
            }

            return hash;
        }
    }

    private int[] CloneIntArray()
    {
        return Clone(intArrayValue);
    }

    private float[] CloneFloatArray()
    {
        return Clone(floatArrayValue);
    }

    private string[] CloneStringArray()
    {
        return Clone(stringArrayValue);
    }

    private static int[] Clone(int[] value)
    {
        return value == null ? Array.Empty<int>() : (int[])value.Clone();
    }

    private static float[] Clone(float[] value)
    {
        return value == null ? Array.Empty<float>() : (float[])value.Clone();
    }

    private static string[] Clone(string[] value)
    {
        return value == null ? Array.Empty<string>() : (string[])value.Clone();
    }
}
