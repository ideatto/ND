using UnityEngine;

[System.Serializable]
public class DisasterInfluenceData
{
    [SerializeField] private Disaster disaster;
    [SerializeField] private bool isInfluence;
    [SerializeField] private float addPrice;
    [SerializeField] private float subtractPrice;
    public Disaster Disaster => disaster;
    public bool IsInfluence => isInfluence;
    public float AddPrice => addPrice;
    public float SubtractPrice => subtractPrice;
}
