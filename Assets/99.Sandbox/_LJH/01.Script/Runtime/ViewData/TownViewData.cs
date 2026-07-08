using UnityEngine;

[System.Serializable]
public class TownViewData
{
    public string townId;
    public string displayName;
    public Sprite icon;
    public string description;

    public bool isUnlocked;
    public bool isCurrentTown;
    public bool canSelect;
    public string disabledReason;
}
