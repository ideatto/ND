using UnityEngine;

[System.Serializable]
public class DraftAnimalViewData
{
    public string draftAnimalId;
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
