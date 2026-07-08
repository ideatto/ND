using UnityEngine;

namespace ND.Framework
{
    public sealed class TradeStartDebugHarness : MonoBehaviour
    {
        [SerializeField] private string tradeId = "debug_trade_001";
        [SerializeField] private string routeId = "debug_route_001";
        [SerializeField] private float distanceKm = 100f;
        [SerializeField] private CaravanData caravan = new CaravanData();

        [ContextMenu("Framework/Fill Sample Caravan")]
        public void FillSampleCaravan()
        {
            caravan = new CaravanData
            {
                wagon = new imsiWagonData
                {
                    wagonName = "Debug Wagon",
                    overLoad = 30f,
                    maxLoad = 60f,
                    minAnimals = 1,
                    maxAnimals = 5
                },
                foodAmount = 30
            };

            caravan.animals.Add(new imsiAnimalData { animalName = "Debug Horse", foodPerKm = 0.1f });
            caravan.animals.Add(new imsiAnimalData { animalName = "Debug Horse", foodPerKm = 0.1f });

            var item = new imsiTradeItemData
            {
                id = "debug_item_wheat",
                itemName = "Debug Wheat",
                weight = 5f,
                basePrice = 10
            };
            caravan.cargo.Add(new CargoEntry { item = item, quantity = 5 });

            FrameworkLog.Info("Sample caravan filled for trade start debug.");
        }

        [ContextMenu("Framework/Start Trade And Record Time")]
        public void StartTradeAndRecordTime()
        {
            if (FrameworkRoot.Instance == null || FrameworkRoot.Instance.TradeStart == null)
            {
                FrameworkLog.Warning("Trade start debug skipped because FrameworkRoot is not ready.");
                return;
            }

            var tradeStart = FrameworkRoot.Instance.TradeStart;
            var result = tradeStart.TryStartTrade(caravan, distanceKm, tradeId, routeId);
            if (!result.canDepart)
            {
                FrameworkLog.Warning($"Debug trade could not depart. Reasons: {string.Join(", ", result.reasons)}");
                return;
            }

            if (tradeStart.LastRecordSucceeded)
            {
                FrameworkLog.Info($"Debug trade started and recorded. TradeId: {tradeId}, RouteId: {routeId}");
                return;
            }

            FrameworkLog.Warning($"Debug trade departed, but start time was not recorded. TradeId: {tradeId}, RouteId: {routeId}");
        }
    }
}
