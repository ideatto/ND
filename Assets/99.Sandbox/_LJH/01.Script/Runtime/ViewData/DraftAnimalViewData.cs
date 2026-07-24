using UnityEngine;

[System.Serializable]
public class DraftAnimalViewData
{
    // Identifies the shared DraftAnimalData definition used for content lookup and display.
    public string draftAnimalId;

    // Identifies one owned animal instance so asset locks do not confuse animals of the same type.
    // Legacy aggregate views may leave this empty, but Caravan setting providers must populate it.
    public string draftAnimalInstanceId;

    public string displayName;
    public Sprite icon;
    public string description;

    public DraftAnimalType animalType;
    public float feedConsumption;
    public float baseMoveSpeed;
    public float increaseOverLoad;
    public float increaseMaxLoad;

    public int ownedAmount;
    public int selectedAmount;
    public int maxSelectableAmount;

    public bool isEligibleForSelectedWagon;
    public bool canSelect;
    public string disabledReason;
}
