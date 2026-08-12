using ND.Framework;
using ND.UI.Quest;
using ND.UI.RescueLoan;
using UnityEngine;

namespace ND.UI.Tutorial
{
    public sealed class TutorialSignalBridge : MonoBehaviour
    {
        [SerializeField] private TutorialSignalStore signalStore;
        [SerializeField] private TownQuestPanelController questPanelController;
        [SerializeField] private SlidePanel worldMapPanel;
        [SerializeField] private BuildingAddPopup buildingAddPopup;
        [SerializeField] private BuildingConstructionRuntimeHandler buildingConstructionHandler;
        [SerializeField] private BuildingPopupRuntimeBinding buildingPopupRuntimeBinding;
        [SerializeField] private CottageProductionPopupPresenter cottageProductionPopup;
        [SerializeField] private TradePrepareUIManager tradePrepareUi;
        [SerializeField] private AnimalInventoryPanel animalInventoryPanel;
        [SerializeField] private CaravanOverviewEditBinding caravanOverviewEditBinding;
        [SerializeField] private ND.UI.Calendar.GameCalendarPopupController calendarPopupController;
        [SerializeField] private ND.UI.InGame.Warehouse.WarehouseInventoryPopupController warehousePopupController;

        private string travelingCaravanId = string.Empty;
        private bool wasWorldMapOpen;
        private bool wasTreadmillOpen;
        private TreadmillPanel treadmillPanel;
        private BuildingPlacementController buildingPlacementController;
        private VillageEnvironmentManager villageEnvironmentManager;
        private ND.UI.Calendar.GameCalendarPopupController boundCalendarPopupController;
        private ND.UI.InGame.SellPriceModifierBuff.SellPriceModifierBuffBarController sellPriceModifierBuffBar;
        private RescueLoanPanelVisibility rescueLoanPanelVisibility;
        private bool wasRescueLoanPanelVisible;

        private void OnEnable()
        {
            ResolveBuildingPlacementController();
            ResolveVillageEnvironmentManager();
            BindCalendarPopupController();
            ResolveSellPriceModifierBuffBar();
            ResolveRescueLoanPanelVisibility();
            wasRescueLoanPanelVisible = rescueLoanPanelVisibility != null
                && rescueLoanPanelVisibility.IsVisible;
            FrameworkEvents.CaravanJourneyStateChanged += HandleJourneyStateChanged;
            FrameworkEvents.QuestRewardsCommitted += HandleQuestRewardsCommitted;
            FrameworkEvents.RescueLoanIssued += HandleRescueLoanIssued;
            if (buildingAddPopup != null)
            {
                buildingAddPopup.Opened += HandleBuildingCatalogOpened;
                buildingAddPopup.Dismissed += HandleBuildingCatalogDismissed;
                buildingAddPopup.BuildingSelected += HandleBuildingSelected;
            }
            if (buildingConstructionHandler != null)
                buildingConstructionHandler.ConstructionCommitted += HandleConstructionCommitted;
            if (buildingPopupRuntimeBinding != null)
            {
                buildingPopupRuntimeBinding.DetailDismissed += HandleBuildingDetailDismissed;
                buildingPopupRuntimeBinding.ConfirmationDismissed += HandleBuildingConfirmationDismissed;
            }
            if (cottageProductionPopup != null)
            {
                cottageProductionPopup.Opened += HandleCottageFunctionOpened;
                cottageProductionPopup.Closed += HandleCottageFunctionDismissed;
                cottageProductionPopup.CollectionSucceeded += HandleCottageCollectionSucceeded;
            }
            if (tradePrepareUi != null)
            {
                tradePrepareUi.OnCaravanSettingOpened += HandleCaravanSettingOpened;
                tradePrepareUi.OnCaravanEditClosed += HandleCaravanSettingClosed;
            }
            if (animalInventoryPanel != null)
            {
                animalInventoryPanel.OnWagonSelected += HandleCaravanWagonSelected;
                animalInventoryPanel.OnSelectionChanged += HandleCaravanAnimalsChanged;
            }
            if (caravanOverviewEditBinding != null)
                caravanOverviewEditBinding.CaravanSettingSaved += HandleCaravanSettingSaved;
            if (warehousePopupController != null)
            {
                warehousePopupController.Opened += HandleWarehouseOpened;
                warehousePopupController.TransferSucceeded += HandleWarehouseTransferSucceeded;
            }
            if (questPanelController != null)
            {
                questPanelController.QuestMenuOpened += HandleQuestMenuOpened;
                questPanelController.QuestMenuClosed += HandleQuestMenuClosed;
                questPanelController.QuestCaravanContextSelected += HandleQuestCaravanSelected;
                questPanelController.QuestPanelChanged += HandleQuestPanelChanged;
                questPanelController.QuestAccepted += HandleQuestAccepted;
                questPanelController.PaymentCaravanSelected += HandlePaymentCaravanSelected;
            }
        }

        private void OnDisable()
        {
            UnbindBuildingPlacementController();
            UnbindVillageEnvironmentManager();
            UnbindCalendarPopupController();
            UnbindSellPriceModifierBuffBar();
            FrameworkEvents.CaravanJourneyStateChanged -= HandleJourneyStateChanged;
            FrameworkEvents.QuestRewardsCommitted -= HandleQuestRewardsCommitted;
            FrameworkEvents.RescueLoanIssued -= HandleRescueLoanIssued;
            if (buildingAddPopup != null)
            {
                buildingAddPopup.Opened -= HandleBuildingCatalogOpened;
                buildingAddPopup.Dismissed -= HandleBuildingCatalogDismissed;
                buildingAddPopup.BuildingSelected -= HandleBuildingSelected;
            }
            if (buildingConstructionHandler != null)
                buildingConstructionHandler.ConstructionCommitted -= HandleConstructionCommitted;
            if (buildingPopupRuntimeBinding != null)
            {
                buildingPopupRuntimeBinding.DetailDismissed -= HandleBuildingDetailDismissed;
                buildingPopupRuntimeBinding.ConfirmationDismissed -= HandleBuildingConfirmationDismissed;
            }
            if (cottageProductionPopup != null)
            {
                cottageProductionPopup.Opened -= HandleCottageFunctionOpened;
                cottageProductionPopup.Closed -= HandleCottageFunctionDismissed;
                cottageProductionPopup.CollectionSucceeded -= HandleCottageCollectionSucceeded;
            }
            if (tradePrepareUi != null)
            {
                tradePrepareUi.OnCaravanSettingOpened -= HandleCaravanSettingOpened;
                tradePrepareUi.OnCaravanEditClosed -= HandleCaravanSettingClosed;
            }
            if (animalInventoryPanel != null)
            {
                animalInventoryPanel.OnWagonSelected -= HandleCaravanWagonSelected;
                animalInventoryPanel.OnSelectionChanged -= HandleCaravanAnimalsChanged;
            }
            if (caravanOverviewEditBinding != null)
                caravanOverviewEditBinding.CaravanSettingSaved -= HandleCaravanSettingSaved;
            if (warehousePopupController != null)
            {
                warehousePopupController.Opened -= HandleWarehouseOpened;
                warehousePopupController.TransferSucceeded -= HandleWarehouseTransferSucceeded;
            }
            if (questPanelController != null)
            {
                questPanelController.QuestMenuOpened -= HandleQuestMenuOpened;
                questPanelController.QuestMenuClosed -= HandleQuestMenuClosed;
                questPanelController.QuestCaravanContextSelected -= HandleQuestCaravanSelected;
                questPanelController.QuestPanelChanged -= HandleQuestPanelChanged;
                questPanelController.QuestAccepted -= HandleQuestAccepted;
                questPanelController.PaymentCaravanSelected -= HandlePaymentCaravanSelected;
            }
        }

        private void Update()
        {
            ResolveBuildingPlacementController();
            ResolveVillageEnvironmentManager();
            BindCalendarPopupController();
            ResolveSellPriceModifierBuffBar();
            ResolveRescueLoanPanelVisibility();
            bool rescueLoanPanelVisible = rescueLoanPanelVisibility != null
                && rescueLoanPanelVisibility.IsVisible;
            if (rescueLoanPanelVisible && !wasRescueLoanPanelVisible)
                Record("loan.panel_opened");
            wasRescueLoanPanelVisible = rescueLoanPanelVisible;
            if (string.IsNullOrEmpty(travelingCaravanId)) return;

            bool worldMapOpen = worldMapPanel != null && worldMapPanel.IsOpen;
            if (worldMapOpen && !wasWorldMapOpen)
                Record("travel.world_map_opened", travelingCaravanId, travelingCaravanId);
            wasWorldMapOpen = worldMapOpen;

            bool treadmillOpen = ResolveTreadmillOpen();
            if (treadmillOpen && !wasTreadmillOpen)
                Record("travel.treadmill_opened", travelingCaravanId, travelingCaravanId);
            wasTreadmillOpen = treadmillOpen;
        }

        private void HandleJourneyStateChanged(string caravanId, JourneyState state)
        {
            string id = caravanId ?? string.Empty;
            if (state == JourneyState.Traveling)
            {
                travelingCaravanId = id;
                wasWorldMapOpen = worldMapPanel != null && worldMapPanel.IsOpen;
                wasTreadmillOpen = ResolveTreadmillOpen();
                Record("travel.departure_succeeded", id, id);
                return;
            }

            if (!string.IsNullOrEmpty(travelingCaravanId)
                && string.Equals(travelingCaravanId, id, System.StringComparison.Ordinal))
            {
                Record("travel.journey_ended", id, id);
                travelingCaravanId = string.Empty;
            }
        }

        private void HandleQuestMenuOpened() => Record("quest.menu_opened");
        private void HandleQuestMenuClosed() => Record("quest.menu_closed");
        private void HandleBuildingCatalogOpened() => Record("building.catalog_opened");
        private void HandleBuildingCatalogDismissed() => Record("building.catalog_dismissed");
        private void HandleBuildingDetailDismissed() => Record("building.detail_dismissed");
        private void HandleBuildingConfirmationDismissed() =>
            Record("building.confirmation_dismissed");
        private void HandleCottageFunctionOpened() =>
            Record("building.cottage_function_opened", "Cottage");
        private void HandleCottageFunctionDismissed() =>
            Record("building.cottage_function_dismissed", "Cottage");
        private void HandleCottageCollectionSucceeded(CottageCollectionTarget target)
        {
            if (target == CottageCollectionTarget.Wagon)
                Record("building.cottage_wagon_collected", "Cottage");
            else if (target == CottageCollectionTarget.DraftAnimal)
                Record("building.cottage_draft_animal_collected", "Cottage");
            else if (target == CottageCollectionTarget.All)
                Record("building.cottage_collect_all_succeeded", "Cottage");
        }
        private void HandleEditModeChanged(bool isEditing) =>
            Record(isEditing ? "building.edit_mode_entered" : "building.edit_mode_exited");
        private void HandleBuildingSelected(string buildId) =>
            Record("building.selected", buildId);
        private void HandleConstructionCommitted(string buildId, int targetLevel) =>
            Record("building.construction_committed", buildId, targetLevel.ToString());
        private void HandleEnvironmentInstalled(string buildId, long cost)
        {
            if (cost > 0)
                Record("building.decoration_installed", buildId, cost.ToString());
        }
        private void HandleCaravanSettingOpened(string caravanId) =>
            Record("caravan.setting_opened", caravanId);
        private void HandleCaravanSettingClosed() =>
            Record("caravan.setting_closed");
        private void HandleCaravanWagonSelected(TransportSelectPanel.TransportEntry wagon) =>
            Record("caravan.wagon_selected", wagon.instanceId, wagon.id);
        private void HandleCaravanAnimalsChanged(
            System.Collections.Generic.IReadOnlyList<AnimalInventoryPanel.AnimalPick> picks,
            bool valid)
        {
            if (!valid || picks == null || picks.Count == 0)
                return;

            Record("caravan.animals_ready");
        }
        private void HandleCaravanSettingSaved(string caravanId) =>
            Record("caravan.setting_saved", caravanId);
        private void HandleWarehouseOpened() =>
            Record("warehouse.panel_opened");
        private void HandleWarehouseTransferSucceeded(WarehouseTransferDirection direction)
        {
            if (direction == WarehouseTransferDirection.CargoToHome)
                Record("warehouse.item_stored");
        }
        private void HandleCalendarOpened()
        {
            Record("calendar.opened");
        }

        private void HandleCalendarClosed()
        {
            Record("calendar.closed");
        }
        private void HandleCalendarValueInspected(ND.UI.Calendar.GameCalendarValueKind value)
        {
            switch (value)
            {
                case ND.UI.Calendar.GameCalendarValueKind.YearMonth:
                    Record("calendar.year_month_inspected");
                    break;
                case ND.UI.Calendar.GameCalendarValueKind.Season:
                    Record("calendar.season_inspected");
                    break;
                case ND.UI.Calendar.GameCalendarValueKind.Disaster:
                    Record("calendar.disaster_inspected");
                    break;
            }
        }

        private void HandleSellPriceModifierBuffInspected(
            ND.UI.InGame.SellPriceModifierBuff.SellPriceModifierBuffKind kind)
        {
            switch (kind)
            {
                case ND.UI.InGame.SellPriceModifierBuff.SellPriceModifierBuffKind.Season:
                    Record("buff.season_inspected");
                    break;
                case ND.UI.InGame.SellPriceModifierBuff.SellPriceModifierBuffKind.Distance:
                    Record("buff.distance_inspected");
                    break;
                case ND.UI.InGame.SellPriceModifierBuff.SellPriceModifierBuffKind.LuckyMoney:
                    Record("buff.lucky_money_inspected");
                    break;
            }
        }
        private void HandleQuestCaravanSelected(string townId, int slotIndex) =>
            Record("quest.caravan_context_selected", townId, slotIndex.ToString());

        private void HandleQuestPanelChanged(string questId, TownQuestPanelKind kind)
        {
            if (kind == TownQuestPanelKind.Offer)
                Record("quest.offer_opened", questId);
            else if (kind == TownQuestPanelKind.Payment)
                Record("quest.payment_opened", questId);
        }

        private void HandleQuestAccepted(string questId) => Record("quest.accepted", questId);
        private void HandlePaymentCaravanSelected(string caravanId) =>
            Record("quest.payment_caravan_selected", string.Empty, caravanId);
        private void HandleQuestRewardsCommitted(QuestRewardsCommittedEvent committed)
        {
            if (committed != null) Record("quest.rewards_committed", committed.QuestId);
        }

        private void HandleRescueLoanIssued(ND.Economy.IssueRescueLoanResult result)
        {
            if (result != null && result.Success)
                Record("loan.fixed_principal_issued", result.LoanId);
        }

        private void Record(string actionId, string contextId = "", string sourceId = "") =>
            signalStore?.Record(actionId, contextId, sourceId);

        private bool ResolveTreadmillOpen()
        {
            if (treadmillPanel == null)
            {
                treadmillPanel = UnityEngine.Object.FindAnyObjectByType<TreadmillPanel>(
                    FindObjectsInactive.Include);
            }
            return treadmillPanel != null && treadmillPanel.IsOpen;
        }

        private void ResolveBuildingPlacementController()
        {
            if (buildingPlacementController != null)
                return;

            buildingPlacementController = UnityEngine.Object.FindAnyObjectByType<BuildingPlacementController>(
                FindObjectsInactive.Include);
            if (buildingPlacementController != null)
                buildingPlacementController.EditModeChanged += HandleEditModeChanged;
        }

        private void UnbindBuildingPlacementController()
        {
            if (buildingPlacementController == null)
                return;

            buildingPlacementController.EditModeChanged -= HandleEditModeChanged;
            buildingPlacementController = null;
        }

        private void ResolveVillageEnvironmentManager()
        {
            VillageEnvironmentManager current = VillageEnvironmentManager.Instance;
            if (current == villageEnvironmentManager)
                return;

            UnbindVillageEnvironmentManager();
            villageEnvironmentManager = current;
            if (villageEnvironmentManager != null)
                villageEnvironmentManager.EnvironmentInstalled += HandleEnvironmentInstalled;
        }

        private void UnbindVillageEnvironmentManager()
        {
            if (villageEnvironmentManager == null)
                return;

            villageEnvironmentManager.EnvironmentInstalled -= HandleEnvironmentInstalled;
            villageEnvironmentManager = null;
        }

        private void BindCalendarPopupController()
        {
            if (boundCalendarPopupController == calendarPopupController)
                return;

            UnbindCalendarPopupController();
            boundCalendarPopupController = calendarPopupController;
            if (boundCalendarPopupController == null)
                return;

            boundCalendarPopupController.Opened += HandleCalendarOpened;
            boundCalendarPopupController.Closed += HandleCalendarClosed;
            boundCalendarPopupController.ValueInspected += HandleCalendarValueInspected;
        }

        private void UnbindCalendarPopupController()
        {
            if (boundCalendarPopupController == null)
                return;

            boundCalendarPopupController.Opened -= HandleCalendarOpened;
            boundCalendarPopupController.Closed -= HandleCalendarClosed;
            boundCalendarPopupController.ValueInspected -= HandleCalendarValueInspected;
            boundCalendarPopupController = null;
        }

        private void ResolveSellPriceModifierBuffBar()
        {
            if (sellPriceModifierBuffBar != null)
                return;

            sellPriceModifierBuffBar = UnityEngine.Object.FindAnyObjectByType<
                ND.UI.InGame.SellPriceModifierBuff.SellPriceModifierBuffBarController>(
                FindObjectsInactive.Include);
            if (sellPriceModifierBuffBar != null)
                sellPriceModifierBuffBar.BuffInspected += HandleSellPriceModifierBuffInspected;
        }

        private void UnbindSellPriceModifierBuffBar()
        {
            if (sellPriceModifierBuffBar == null)
                return;

            sellPriceModifierBuffBar.BuffInspected -= HandleSellPriceModifierBuffInspected;
            sellPriceModifierBuffBar = null;
        }

        private void ResolveRescueLoanPanelVisibility()
        {
            if (rescueLoanPanelVisibility != null)
                return;

            rescueLoanPanelVisibility = UnityEngine.Object.FindAnyObjectByType<
                RescueLoanPanelVisibility>(FindObjectsInactive.Include);
            wasRescueLoanPanelVisible = rescueLoanPanelVisibility != null
                && rescueLoanPanelVisibility.IsVisible;
        }
    }
}
