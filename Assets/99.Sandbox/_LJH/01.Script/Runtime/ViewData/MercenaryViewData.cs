using UnityEngine;

[System.Serializable]
public class MercenaryViewData
{
    public string mercenaryId;
    public string displayName;
    public Sprite icon;
    public string description;

    public int combatCapability;
    public long baseBuyPrice;

    public bool isSelected;

    public bool canHire;
    public string disabledReason;
}
