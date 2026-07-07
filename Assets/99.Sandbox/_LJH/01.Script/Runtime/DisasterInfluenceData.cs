using UnityEngine;

[System.Serializable]
public class DisasterInfluenceData
{
    [SerializeField] private Disaster disaster;
    [SerializeField] private float priceFluctuationRatio;

    #region
    public Disaster Disaster => disaster;
    public float PriceFluctuationRatio => priceFluctuationRatio;
    #endregion
}
