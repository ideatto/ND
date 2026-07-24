using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class DualGrowthCommandTests
    {
        [Test]
        public void Execute_ValidGrowth_CompletesAfterSave()
        {
            var port = new FakePort();

            DualGrowthCommandResult result =
                DualGrowthCommand.Execute(Input(), port);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailurePhase,
                Is.EqualTo(DualGrowthCommandFailurePhase.None));
            Assert.That(result.SaveSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.True);
            Assert.That(result.SuccessEventPublished, Is.True);
            Assert.That(result.Plan.Axis, Is.EqualTo(GrowthAxis.Player));
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "commit", "event" }));
        }

        [Test]
        public void Execute_ValidationFailure_DoesNotCallPersistence()
        {
            var port = new FakePort();
            DualGrowthInput input = Input();
            input.DevelopmentCurrency = 1;

            DualGrowthCommandResult result =
                DualGrowthCommand.Execute(input, port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailurePhase,
                Is.EqualTo(DualGrowthCommandFailurePhase.Validation));
            Assert.That(result.ValidationFailureReason,
                Is.EqualTo(DualGrowthFailureReason.InsufficientDevelopmentCurrency));
            Assert.That(result.ErrorCode,
                Is.EqualTo("GROWTH_INSUFFICIENT_DEVELOPMENT_CURRENCY"));
            Assert.That(result.Plan, Is.Null);
            Assert.That(port.Calls, Is.Empty);
        }

        [Test]
        public void Execute_SaveFailure_IsSeparatedFromValidation()
        {
            var port = new FakePort { SaveSucceeds = false };

            DualGrowthCommandResult result =
                DualGrowthCommand.Execute(Input(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailurePhase,
                Is.EqualTo(DualGrowthCommandFailurePhase.Persistence));
            Assert.That(result.ValidationFailureReason,
                Is.EqualTo(DualGrowthFailureReason.None));
            Assert.That(result.TransactionFailureReason,
                Is.EqualTo(DualGrowthTransactionFailureReason.SaveFailed));
            Assert.That(result.ErrorCode, Is.EqualTo("DISK_WRITE_FAILED"));
            Assert.That(result.RuntimeCommitted, Is.False);
            Assert.That(result.SuccessEventPublished, Is.False);
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "rollback" }));
        }

        [Test]
        public void Execute_MissingPort_ReturnsPersistenceFailure()
        {
            DualGrowthCommandResult result =
                DualGrowthCommand.Execute(Input(), null);

            Assert.That(result.FailurePhase,
                Is.EqualTo(DualGrowthCommandFailurePhase.Persistence));
            Assert.That(result.TransactionFailureReason,
                Is.EqualTo(DualGrowthTransactionFailureReason.InvalidInput));
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_INPUT"));
        }

        private static DualGrowthInput Input()
        {
            return new DualGrowthInput
            {
                RequestedGrowthId = "player_capacity",
                RequestedAxis = GrowthAxis.Player,
                PlayerGrowthLevel = 1,
                CaravanGrowthLevel = 3,
                DevelopmentCurrency = 100,
                Definition = new GrowthAxisDefinition
                {
                    GrowthId = "player_capacity",
                    Axis = GrowthAxis.Player,
                    MaximumLevel = 2,
                    Levels =
                    {
                        new GrowthLevelDefinition
                        {
                            Level = 2,
                            DevelopmentCurrencyCost = 20,
                            Effects =
                            {
                                new GrowthLevelEffect
                                {
                                    EffectId = "player_capacity",
                                    Value = 10
                                }
                            }
                        }
                    }
                }
            };
        }

        private sealed class Snapshot : IDualGrowthTransactionSnapshot
        {
        }

        private sealed class FakePort : IDualGrowthTransactionPort
        {
            public readonly List<string> Calls = new List<string>();
            public bool SaveSucceeds = true;

            public IDualGrowthTransactionSnapshot CaptureSnapshot()
            {
                Calls.Add("snapshot");
                return new Snapshot();
            }

            public bool TryStage(
                DualGrowthEconomicPlan plan,
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
                IDualGrowthTransactionSnapshot snapshot,
                out string errorCode)
            {
                Calls.Add("rollback");
                errorCode = string.Empty;
                return true;
            }

            public void CommitRuntime(DualGrowthEconomicPlan plan)
            {
                Calls.Add("commit");
            }

            public void PublishSuccess(DualGrowthEconomicPlan plan)
            {
                Calls.Add("event");
            }
        }
    }
}
