using System.Diagnostics.Contracts;
using UnityEngine;

[System.Serializable]
public class SeasonInfluenceData
{
    [SerializeField] private Season season;
    [SerializeField] private bool isInfluenced;
    [SerializeField] private int influencePrice;

    public Season Season => season;
    public bool IsInfluenced => isInfluenced;
    public int InfluencePrice => influencePrice;
}
