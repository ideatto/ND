using System.Collections.Generic;

[System.Serializable]
public class TradePrepareConditionResult
{
    public bool canStart;
    public string disabledReason;

    public bool hasWarning;
    public List<string> warningMessages = new List<string>();
}
