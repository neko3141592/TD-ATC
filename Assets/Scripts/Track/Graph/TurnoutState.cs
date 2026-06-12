using System;

public enum TurnoutPosition
{
    Normal,
    Reverse,
}

[Serializable]
public class TurnoutState
{
    public string junctionId;

    public string normalConnectionId;
    public string reverseConnectionId;

    public TurnoutPosition selectedPosition = TurnoutPosition.Normal;

    public string ActiveConnectionId =>
        selectedPosition == TurnoutPosition.Normal
            ? normalConnectionId
            : reverseConnectionId;
}
