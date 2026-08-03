using System;
using System.Collections.Generic;
using ND.Framework;
using UnityEngine;

/// <summary>
/// Supplies Caravan Setting with transport definitions without coupling the UI to asset discovery.
/// Replace this provider with an owned-inventory provider when transport ownership is implemented.
/// </summary>
public sealed class CaravanTransportCatalogProvider
{
    private readonly Dictionary<string, WagonData> wagons =
        new Dictionary<string, WagonData>(StringComparer.Ordinal);
    private readonly Dictionary<string, DraftAnimalData> animals =
        new Dictionary<string, DraftAnimalData>(StringComparer.Ordinal);

    public CaravanTransportCatalogProvider(SandboxSharedGameDataCatalog catalog = null)
    {
        catalog = catalog ?? Resources.Load<SandboxSharedGameDataCatalog>(
            SandboxSharedGameDataCatalog.ResourceName);
        if (catalog == null)
            return;

        WagonData[] wagonAssets = catalog.Wagons;
        for (int index = 0; index < wagonAssets.Length; index++)
        {
            WagonData asset = wagonAssets[index];
            string id = NormalizeId(asset != null ? asset.WagonId : null);
            if (!string.IsNullOrEmpty(id) && !wagons.ContainsKey(id))
                wagons.Add(id, asset);
        }

        DraftAnimalData[] animalAssets = catalog.DraftAnimals;
        for (int index = 0; index < animalAssets.Length; index++)
        {
            DraftAnimalData asset = animalAssets[index];
            string id = NormalizeId(asset != null ? asset.DraftAnimalId : null);
            if (!string.IsNullOrEmpty(id) && !animals.ContainsKey(id))
                animals.Add(id, asset);
        }
    }

    public IEnumerable<WagonData> Wagons => wagons.Values;
    public IEnumerable<DraftAnimalData> DraftAnimals => animals.Values;

    public bool TryGetWagon(string wagonId, out WagonData wagon) =>
        wagons.TryGetValue(NormalizeId(wagonId), out wagon);

    public bool TryGetDraftAnimal(string animalId, out DraftAnimalData animal) =>
        animals.TryGetValue(NormalizeId(animalId), out animal);

    private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
}