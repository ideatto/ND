using UnityEngine;

[System.Serializable]
public class DisasterInfluenceData
{
    [SerializeField] private Disaster disaster;
    [SerializeField] private bool isInfluence;
    [SerializeField] private float influencePrice;

    public Disaster Disaster => disaster;
    public bool IsInfluence => isInfluence;
    public float InfluencePrice => influencePrice;
}
