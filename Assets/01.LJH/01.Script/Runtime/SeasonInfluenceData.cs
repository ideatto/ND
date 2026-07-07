using UnityEngine;

[System.Serializable]
public class SeasonInfluenceData
{
    [SerializeField] private Season season;
    [SerializeField] private int addPrice;
    [SerializeField] private int subtractPrice;

    public Season Season => season;
    public int AddPrice => addPrice;
    public int SubtractPrice => subtractPrice;
}
