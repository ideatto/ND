using UnityEngine;

[System.Serializable]
public class SeasonInfluenceData
{
    [SerializeField] private Season season;
    [SerializeField] private float priceFluctuationRatio;

    #region
    public Season Season => season;
    public float PriceFluctuationRatio => priceFluctuationRatio;
    #endregion
}
