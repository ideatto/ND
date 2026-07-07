using UnityEngine;

[System.Serializable]
public class SeasonInfluenceData
{
    [SerializeField] private Season season;
    [SerializeField] private bool isInfluenced;
    [SerializeField] private int addPrice;
    [SerializeField] private int subtractPrice;

    public Season Season => season;
    public bool IsInfluenced => isInfluenced;
    public int AddPrice => addPrice;
    public int SubtractPrice => subtractPrice;
}
