using UnityEngine;

[System.Serializable]
public class WagonViewData
{
    // Identifies the shared WagonData definition used for content lookup and display.
    public string wagonId;

    // Identifies one owned wagon instance so two wagons using the same WagonData remain distinct.
    // Legacy aggregate views may leave this empty, but Caravan setting providers must populate it.
    public string wagonInstanceId;

    public string displayName;
    public Sprite icon;
    public string description;

    public WagonType wagonType;
    public float baseMoveSpeed;

    public int currentDurability;
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
