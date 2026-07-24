using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class BuildingUpgradeCommandTests
    {
        [Test]
        public void Execute_ValidUpgrade_CompletesAfterSave()
        {
            var port = new FakePort();

            BuildingUpgradeCommandResult result =
                BuildingUpgradeCommand.Execute(Input(), port);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailurePhase,
                Is.EqualTo(BuildingUpgradeCommandFailurePhase.None));
            Assert.That(result.SaveSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.True);
            Assert.That(result.SuccessEventPublished, Is.True);
            Assert.That(result.Plan.BuildingId, Is.EqualTo("warehouse"));
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "commit", "event" }));
        }

        [Test]
        public void Execute_ValidationFailure_DoesNotCallPersistence()
        {
            var port = new FakePort();
            BuildingUpgradeInput input = Input();
            input.HomeInventory[0].Quantity = 1;

            BuildingUpgradeCommandResult result =
                BuildingUpgradeCommand.Execute(input, port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailurePhase,
                Is.EqualTo(BuildingUpgradeCommandFailurePhase.Validation));
            Assert.That(result.ValidationFailureReason,
                Is.EqualTo(BuildingUpgradeFailureReason.InsufficientMaterials));
            Assert.That(result.ErrorCode,
                Is.EqualTo("BUILDING_INSUFFICIENT_MATERIALS"));
            Assert.That(result.Plan, Is.Null);
            Assert.That(port.Calls, Is.Empty);
        }

        [Test]
        public void Execute_SaveFailure_IsSeparatedFromValidationFailure()
        {
            var port = new FakePort { SaveSucceeds = false };

            BuildingUpgradeCommandResult result =
                BuildingUpgradeCommand.Execute(Input(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailurePhase,
                Is.EqualTo(BuildingUpgradeCommandFailurePhase.Persistence));
            Assert.That(result.ValidationFailureReason,
                Is.EqualTo(BuildingUpgradeFailureReason.None));
            Assert.That(result.TransactionFailureReason,
                Is.EqualTo(BuildingUpgradeTransactionFailureReason.SaveFailed));
            Assert.That(result.ErrorCode, Is.EqualTo("DISK_WRITE_FAILED"));
            Assert.That(result.RuntimeCommitted, Is.False);
            Assert.That(result.SuccessEventPublished, Is.False);
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "rollback" }));
        }

        [Test]
        public void Execute_MissingPort_ReturnsPersistenceFailure()
        {
            BuildingUpgradeCommandResult result =
                BuildingUpgradeCommand.Execute(Input(), null);

            Assert.That(result.FailurePhase,
                Is.EqualTo(BuildingUpgradeCommandFailurePhase.Persistence));
            Assert.That(result.TransactionFailureReason,
                Is.EqualTo(BuildingUpgradeTransactionFailureReason.InvalidInput));
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_INPUT"));
        }

        private static BuildingUpgradeInput Input()
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
                            },
                            Effects =
                            {
                                new BuildingUpgradeEffect
                                {
                                    EffectId = "storage_slots",
                                    Value = 10
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

        private sealed class Snapshot : IBuildingUpgradeTransactionSnapshot
        {
        }

        private sealed class FakePort : IBuildingUpgradeTransactionPort
        {
            public readonly List<string> Calls = new List<string>();
            public bool SaveSucceeds = true;

            public IBuildingUpgradeTransactionSnapshot CaptureSnapshot()
            {
                Calls.Add("snapshot");
                return new Snapshot();
            }

            public bool TryStage(
                BuildingUpgradeEconomicPlan plan,
                out string errorCode)
            {
                Calls.Add("stage");
                errorCode = string.Empty;
                return true;
            }

            public bool TrySave(out string errorCode)
            {
                Calls.Add("save");
                errorCode = SaveSucceeds ? string.Empty : "DISK_WRITE_FAILED";
                return SaveSucceeds;
            }

            public bool TryRollback(
                IBuildingUpgradeTransactionSnapshot snapshot,
                out string errorCode)
            {
                Calls.Add("rollback");
                errorCode = string.Empty;
                return true;
            }

            public void CommitRuntime(BuildingUpgradeEconomicPlan plan)
            {
                Calls.Add("commit");
            }

            public void PublishSuccess(BuildingUpgradeEconomicPlan plan)
            {
                Calls.Add("event");
            }
        }
    }
}
