using System;
using ND.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Adds Inspector-configured building materials to the saved home inventory.</summary>
[RequireComponent(typeof(Button))]
public sealed class BuildingMaterialTestButton : MonoBehaviour
{
    [Serializable]
    private sealed class GrantItem
    {
        public TradeItemData item;
        [Min(0)] public long purchaseUnitPrice;
    }

    [SerializeField] private GrantItem[] items = Array.Empty<GrantItem>();
    [SerializeField, Min(1)] private int grantQuantity = 5;

    private void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GetComponent<Button>().onClick.AddListener(GrantMaterials);
#else
        gameObject.SetActive(false);
#endif
    }

    public void GrantMaterials()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PlayerMainManager player = PlayerMainManager.Instance;
        if (player == null)
        {
            Debug.LogWarning("[Building Debug] Player inventory is unavailable.", this);
            return;
        }

        int quantity = Mathf.Max(1, grantQuantity);
        foreach (GrantItem entry in items)
        {
            if (entry?.item == null)
                continue;

            player.AddItem(CreateSaveItem(entry), quantity);
        }
#endif
    }

    private static TradeItemSaveData CreateSaveItem(GrantItem entry)
    {
        TradeItemData item = entry.item;
        return new TradeItemSaveData
        {
            itemId = item.ItemId,
            itemName = item.DisplayName,
            weight = item.Weight,
            basePrice = item.BaseBuyPrice,
            purchaseUnitPrice = Math.Max(0L, entry.purchaseUnitPrice),
            maxCount = item.MaxCount
        };
    }
}
