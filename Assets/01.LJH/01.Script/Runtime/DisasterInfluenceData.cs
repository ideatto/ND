using UnityEngine;

[System.Serializable]
public class DisasterInfluenceData
{
    [SerializeField] private Disaster disaster;
    [SerializeField] private float addPrice;
    [SerializeField] private float subtractPrice;

    #region
    public Disaster Disaster => disaster;
    public float AddPrice => addPrice;
    public float SubtractPrice => subtractPrice;
    #endregion
}
