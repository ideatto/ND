using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class ProgressionSecondBuildRegressionTests
    {
        [Test]
        public void Repair_SaveFailure_NeverCommitsRuntimeOrEvent()
        {
            var port = new FailingRepairPort();
            WagonRepairInput input = new WagonRepairInput
            {
                CurrentDurability = 50,
                MaximumDurability = 100,
                RequestedRepairAmount = 10,
                RepairCostPerDurability = 2,
                RarityMultiplier = 1,
                TradingCurrency = 100
            };

            WagonRepairCommandResult result =
                WagonRepairCommand.Execute("caravan-regression", input, port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.TransactionFailureReason,
                Is.EqualTo(WagonRepairTransactionFailureReason.SaveFailed));
            Assert.That(result.RuntimeCommitted, Is.False);
            Assert.That(result.SuccessEventPublished, Is.False);
            Assert.That(port.RolledBack, Is.True);
        }

        [Test]
        public void Building_MissingMaterial_NeverCreatesExecutionPlan()
        {
            BuildingUpgradeInput input = BuildingInput();
            input.HomeInventory[0].Quantity = 1;

            BuildingUpgradePlanBuildResult result =
                BuildingUpgradeEconomicPlanBuilder.Build(input);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeFailureReason.InsufficientMaterials));
            Assert.That(result.Plan, Is.Null);
            Assert.That(input.HomeInventory[0].Quantity, Is.EqualTo(1));
        }

        [Test]
        public void Growth_PlayerPurchase_NeverChangesCaravanAxis()
        {
            DualGrowthInput input = GrowthInput();

            DualGrowthPlanBuildResult result =
                DualGrowthEconomicPlanBuilder.Build(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Plan.PlayerGrowthLevelBefore, Is.EqualTo(1));
            Assert.That(result.Plan.PlayerGrowthLevelAfter, Is.EqualTo(2));
            Assert.That(result.Plan.CaravanGrowthLevelBefore, Is.EqualTo(4));
            Assert.That(result.Plan.CaravanGrowthLevelAfter, Is.EqualTo(4));
        }

        [Test]
        public void Investment_InsufficientItem_NeverReturnsPartialCompletion()
        {
            InvestmentQuestInput input = InvestmentInput();
            input.CaravanInventory[0].Quantity = 1;

            InvestmentQuestPlanBuildResult result =
                InvestmentQuestEconomicPlanBuilder.Build(input);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestFailureReason.InsufficientItems));
            Assert.That(result.Plan, Is.Null);
            Assert.That(input.TradingCurrency, Is.EqualTo(500));
            Assert.That(input.CaravanInventory[0].Quantity, Is.EqualTo(1));
        }

        [Test]
        public void JourneyModifiers_DuplicateRejectedAndExtremeProfitCapped()
        {
            JourneyEconomyModifierInput duplicate = ModifierInput();
            duplicate.Modifiers.Add(Modifier("storm", 2));
            duplicate.Modifiers.Add(Modifier("storm", 2));

            JourneyEconomyModifierResult duplicateResult =
                JourneyEconomyModifierCalculator.Evaluate(duplicate);

            Assert.That(duplicateResult.FailureReason,
                Is.EqualTo(JourneyModifierFailureReason.DuplicateModifier));

            JourneyEconomyModifierInput extreme = ModifierInput();
            extreme.Modifiers.Add(Modifier("storm-a", double.MaxValue));
            extreme.Modifiers.Add(Modifier("storm-b", double.MaxValue));

            JourneyEconomyModifierResult extremeResult =
                JourneyEconomyModifierCalculator.Evaluate(extreme);

            Assert.That(extremeResult.Success, Is.True);
            Assert.That(extremeResult.CombinedPriceFactor, Is.EqualTo(10));
            Assert.That(extremeResult.AdjustedUnitPrice, Is.EqualTo(1000));
        }

        private static BuildingUpgradeInput BuildingInput()
        {
            return new BuildingUpgradeInput
            {
                BuildingId = "warehouse",
                Definition = new BuildingUpgradeDefinition
                {
                    BuildingId = "warehouse",
                    MaximumLevel = 1,
                    Levels =
                    {
                        new BuildingUpgradeLevelDefinition
                        {
                            Level = 1,
                            Materials =
                            {
                                new BuildingUpgradeMaterialRequirement
                                {
                                    ItemId = "wood",
                                    Quantity = 3
                                }
                            }
                        }
                    }
                },
                HomeInventory =
                {
                    new BuildingUpgradeInventoryEntry
                    {
                        ItemId = "wood",
                        Quantity = 5
                    }
                }
            };
        }

        private static DualGrowthInput GrowthInput()
        {
            return new DualGrowthInput
            {
                RequestedGrowthId = "player-capacity",
                RequestedAxis = GrowthAxis.Player,
                PlayerGrowthLevel = 1,
                CaravanGrowthLevel = 4,
                DevelopmentCurrency = 100,
                Definition = new GrowthAxisDefinition
                {
                    GrowthId = "player-capacity",
                    Axis = GrowthAxis.Player,
                    MaximumLevel = 2,
                    Levels =
                    {
                        new GrowthLevelDefinition
                        {
                            Level = 2,
                            DevelopmentCurrencyCost = 20
                        }
                    }
                }
            };
        }

        private static InvestmentQuestInput InvestmentInput()
        {
            return new InvestmentQuestInput
            {
                RequestedQuestId = "invest-regression",
                CaravanId = "caravan-regression",
                CanSubmitCaravanAssets = true,
                TradingCurrency = 500,
                Definition = new InvestmentQuestDefinition
                {
                    QuestId = "invest-regression",
                    TradingCurrencyCost = 100,
                    ItemCosts =
                    {
                        new InvestmentItemCost
                        {
                            ItemId = "iron",
                            Quantity = 2
                        }
                    },
                    UnlockRouteIds = { "route-regression" }
                },
                CaravanInventory =
                {
                    new InvestmentInventoryEntry
                    {
                        ItemId = "iron",
                        Quantity = 3
                    }
                }
            };
        }

        private static JourneyEconomyModifierInput ModifierInput()
        {
            return new JourneyEconomyModifierInput
            {
                BaseUnitPrice = 100,
                BaseSpeed = 10,
                BaseFoodConsumption = 10,
                BaseRiskRate = 0.1,
                BaseLossRate = 0.1
            };
        }

        private static JourneyEconomyModifier Modifier(
            string sourceId,
            double priceFactor)
        {
            return new JourneyEconomyModifier
            {
                SourceType = JourneyModifierSourceType.Disaster,
                SourceId = sourceId,
                PriceFactor = priceFactor
            };
        }

        private sealed class RepairSnapshot : IWagonRepairTransactionSnapshot
        {
        }

        private sealed class FailingRepairPort : IWagonRepairTransactionPort
        {
            public bool RolledBack;

            public IWagonRepairTransactionSnapshot CaptureSnapshot()
            {
                return new RepairSnapshot();
            }

            public bool TryStage(
                WagonRepairEconomicPlan plan,
                out string errorCode)
            {
                errorCode = string.Empty;
                return true;
            }

            public bool TrySave(out string errorCode)
            {
                errorCode = "REGRESSION_SAVE_FAILURE";
                return false;
            }

            public bool TryRollback(
                IWagonRepairTransactionSnapshot snapshot,
                out string errorCode)
            {
                RolledBack = true;
                errorCode = string.Empty;
                return true;
            }

            public void CommitRuntime(WagonRepairEconomicPlan plan)
            {
                Assert.Fail("Runtime commit must not run after save failure.");
            }

            public void PublishSuccess(WagonRepairEconomicPlan plan)
            {
                Assert.Fail("Success event must not run after save failure.");
            }
        }
    }
}
