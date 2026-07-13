using UnityEngine;

[System.Serializable]
public class WagonViewData
{
    public string wagonId;
    public string displayName;
    public Sprite icon;
    public string description;

    public WagonType wagonType;
    public float baseMoveSpeed;

    public float currentDurability;
    public int maxDurability;

    public float overLoad;
    public float maxLoad;
    public int inventorySlotCount;

    public DraftAnimalType[] eligibleAnimalTypes;
    public int minRequireAnimals;
    public int maxPullAnimals;

    public int ownedAmount;
    public bool isOwned;
    public bool canSelect;
    public string disabledReason;
}
