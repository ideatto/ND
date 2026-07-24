using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class WagonRepairTransactionContractTests
    {
        [Test]
        public void Execute_Success_UsesRequiredOrdering()
        {
            var port = new FakePort();

            WagonRepairTransactionResult result =
                WagonRepairTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(port.Calls, Is.EqualTo(new[]
            {
                "snapshot", "stage", "save", "commit", "event"
            }));
        }

        [Test]
        public void Execute_SaveFailure_RollsBackAndPublishesNothing()
        {
            var port = new FakePort { SaveSucceeds = false };

            WagonRepairTransactionResult result =
                WagonRepairTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(WagonRepairTransactionFailureReason.SaveFailed));
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.False);
            Assert.That(result.SuccessEventPublished, Is.False);
            Assert.That(port.Calls, Is.EqualTo(new[] { "snapshot", "stage", "save", "rollback" }));
        }

        [Test]
        public void Execute_StageFailure_RollsBackBeforeSave()
        {
            var port = new FakePort { StageSucceeds = false };

            WagonRepairTransactionResult result =
                WagonRepairTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason, Is.EqualTo(WagonRepairTransactionFailureReason.StageFailed));
            Assert.That(port.Calls, Is.EqualTo(new[] { "snapshot", "stage", "rollback" }));
        }

        [Test]
        public void Execute_RollbackFailure_ReportsRollbackFailure()
        {
            var port = new FakePort
            {
                SaveSucceeds = false,
                RollbackSucceeds = false
            };

            WagonRepairTransactionResult result =
                WagonRepairTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason, Is.EqualTo(WagonRepairTransactionFailureReason.RollbackFailed));
            Assert.That(result.ErrorCode, Is.EqualTo("ROLLBACK_FAILED"));
        }

        private static WagonRepairEconomicPlan Plan()
        {
            return new WagonRepairEconomicPlan("caravan-1", 50, 75, 25, 50, 100, 50);
        }

        private sealed class Snapshot : IWagonRepairTransactionSnapshot
        {
        }

        private sealed class FakePort : IWagonRepairTransactionPort
        {
            public readonly List<string> Calls = new List<string>();
            public bool StageSucceeds = true;
            public bool SaveSucceeds = true;
            public bool RollbackSucceeds = true;

            public IWagonRepairTransactionSnapshot CaptureSnapshot()
            {
                Calls.Add("snapshot");
                return new Snapshot();
            }

            public bool TryStage(WagonRepairEconomicPlan plan, out string errorCode)
            {
                Calls.Add("stage");
                errorCode = StageSucceeds ? string.Empty : "STAGE_FAILED";
                return StageSucceeds;
            }

            public bool TrySave(out string errorCode)
            {
                Calls.Add("save");
                errorCode = SaveSucceeds ? string.Empty : "SAVE_FAILED";
                return SaveSucceeds;
            }

            public bool TryRollback(IWagonRepairTransactionSnapshot snapshot, out string errorCode)
            {
                Calls.Add("rollback");
                errorCode = RollbackSucceeds ? string.Empty : "ROLLBACK_FAILED";
                return RollbackSucceeds;
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
