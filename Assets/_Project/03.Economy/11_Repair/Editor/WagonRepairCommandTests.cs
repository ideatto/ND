using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class WagonRepairCommandTests
    {
        [Test]
        public void Execute_ValidRequest_CompletesSavedRepair()
        {
            var port = new FakePort();

            WagonRepairCommandResult result =
                WagonRepairCommand.Execute("caravan-3", ValidInput(), port);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailurePhase, Is.EqualTo(WagonRepairCommandFailurePhase.None));
            Assert.That(result.SaveSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.True);
            Assert.That(result.SuccessEventPublished, Is.True);
            Assert.That(result.Plan.CaravanId, Is.EqualTo("caravan-3"));
            Assert.That(result.Plan.RepairCost, Is.EqualTo(50));
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "commit", "event" }));
        }

        [Test]
        public void Execute_ValidationFailure_DoesNotCallPersistencePort()
        {
            var port = new FakePort();
            WagonRepairInput input = ValidInput();
            input.CurrentDurability = 100;

            WagonRepairCommandResult result =
                WagonRepairCommand.Execute("caravan-3", input, port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailurePhase, Is.EqualTo(WagonRepairCommandFailurePhase.Validation));
            Assert.That(result.ValidationFailureReason, Is.EqualTo(WagonRepairFailureReason.AlreadyFull));
            Assert.That(result.ErrorCode, Is.EqualTo("REPAIR_ALREADY_FULL"));
            Assert.That(result.Plan, Is.Null);
            Assert.That(port.Calls, Is.Empty);
        }

        [Test]
        public void Execute_SaveFailure_IsReportedSeparatelyFromValidation()
        {
            var port = new FakePort { SaveSucceeds = false };

            WagonRepairCommandResult result =
                WagonRepairCommand.Execute("caravan-3", ValidInput(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailurePhase, Is.EqualTo(WagonRepairCommandFailurePhase.Persistence));
            Assert.That(result.ValidationFailureReason, Is.EqualTo(WagonRepairFailureReason.None));
            Assert.That(result.TransactionFailureReason, Is.EqualTo(WagonRepairTransactionFailureReason.SaveFailed));
            Assert.That(result.ErrorCode, Is.EqualTo("DISK_WRITE_FAILED"));
            Assert.That(result.RuntimeCommitted, Is.False);
            Assert.That(result.SuccessEventPublished, Is.False);
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "rollback" }));
        }

        [Test]
        public void Execute_MissingPort_ReturnsPersistenceInvalidInput()
        {
            WagonRepairCommandResult result =
                WagonRepairCommand.Execute("caravan-3", ValidInput(), null);

            Assert.That(result.FailurePhase, Is.EqualTo(WagonRepairCommandFailurePhase.Persistence));
            Assert.That(result.TransactionFailureReason, Is.EqualTo(WagonRepairTransactionFailureReason.InvalidInput));
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_INPUT"));
        }

        private static WagonRepairInput ValidInput()
        {
            return new WagonRepairInput
            {
                CurrentDurability = 50,
                MaximumDurability = 100,
                RequestedRepairAmount = 25,
                RepairCostPerDurability = 2,
                RarityMultiplier = 1,
                TradingCurrency = 100
            };
        }

        private sealed class Snapshot : IWagonRepairTransactionSnapshot
        {
        }

        private sealed class FakePort : IWagonRepairTransactionPort
        {
            public readonly List<string> Calls = new List<string>();
            public bool SaveSucceeds = true;

            public IWagonRepairTransactionSnapshot CaptureSnapshot()
            {
                Calls.Add("snapshot");
                return new Snapshot();
            }

            public bool TryStage(WagonRepairEconomicPlan plan, out string errorCode)
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

            public bool TryRollback(IWagonRepairTransactionSnapshot snapshot, out string errorCode)
            {
                Calls.Add("rollback");
                errorCode = string.Empty;
                return true;
            }

            public void CommitRuntime(WagonRepairEconomicPlan plan)
            {
                Calls.Add("commit");
            }

            public void PublishSuccess(WagonRepairEconomicPlan plan)
            {
                Calls.Add("event");
            }
        }
    }
}
