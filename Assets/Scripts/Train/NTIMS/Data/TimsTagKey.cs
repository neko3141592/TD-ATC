using System;

public readonly struct TimsTagKey : IEquatable<TimsTagKey>
{
    public readonly string DeviceName;
    public readonly string ItemName;

    public TimsTagKey (string deviceName, string itemName)
    {
        DeviceName = deviceName;
        ItemName = itemName;
    }

    public bool Equals(TimsTagKey other)
    {
        return 
            DeviceName == other.DeviceName &&
            ItemName == other.ItemName;
    }

    public override bool Equals(object obj)
    {
        return obj is TimsTagKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(DeviceName, ItemName);
    }

    public override string ToString()
    {
        return $"{DeviceName}.{ItemName}";
    }


}
