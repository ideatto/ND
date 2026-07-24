using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class SettlementClaimTransactionContractTests
    {
        [Test]
        public void Execute_UsesRequiredSuccessOrder()
        {
            var port = new FakePort();
            SettlementClaimTransactionResult result = SettlementClaimTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SaveSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.True);
            Assert.That(result.SuccessEventPublished, Is.True);
            Assert.That(port.Calls, Is.EqualTo(new[] { "snapshot", "stage", "save", "commit", "event" }));
        }

        [Test]
        public void Execute_SaveFailureRollsBackAndSuppressesCommitAndEvent()
        {
            var port = new FakePort { SaveSucceeds = false };
            SettlementClaimTransactionResult result = SettlementClaimTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(SettlementClaimTransactionFailureReason.SaveFailed));
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.False);
            Assert.That(result.SuccessEventPublished, Is.False);
            Assert.That(port.Calls, Is.EqualTo(new[] { "snapshot", "stage", "save", "rollback" }));
        }

        [Test]
        public void Execute_PartialStageFailureStillRollsBack()
        {
            var port = new FakePort { StageSucceeds = false };
            SettlementClaimTransactionResult result = SettlementClaimTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason, Is.EqualTo(SettlementClaimTransactionFailureReason.StageFailed));
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(port.Calls, Is.EqualTo(new[] { "snapshot", "stage", "rollback" }));
        }

        [Test]
        public void Execute_ReportsRollbackFailureAndNeverPublishesSuccess()
        {
            var port = new FakePort { SaveSucceeds = false, RollbackSucceeds = false };
            SettlementClaimTransactionResult result = SettlementClaimTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason, Is.EqualTo(SettlementClaimTransactionFailureReason.RollbackFailed));
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.False);
            Assert.That(result.SuccessEventPublished, Is.False);
        }

        [Test]
        public void Execute_RuntimeCommitFailureOccursOnlyAfterDurableSaveAndSuppressesEvent()
        {
            var port = new FakePort { ThrowOnCommit = true };
            SettlementClaimTransactionResult result = SettlementClaimTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.SaveSucceeded, Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(SettlementClaimTransactionFailureReason.RuntimeCommitFailed));
            Assert.That(result.SuccessEventPublished, Is.False);
            Assert.That(port.Calls, Is.EqualTo(new[] { "snapshot", "stage", "save", "commit" }));
        }

        private static SettlementClaimEconomicPlan Plan()
        {
            return new SettlementClaimEconomicPlan(
                "caravan-a", "trade-a", 300L, 150L, 150L,
                1000L, 1150L, 120L, 1030L,
                120L, 0L, false, false, false, 0L);
        }

        private sealed class FakeSnapshot : ISettlementClaimTransactionSnapshot
        {
        }

        private sealed class FakePort : ISettlementClaimTransactionPort
        {
            public readonly List<string> Calls = new List<string>();
            public bool StageSucceeds = true;
            public bool SaveSucceeds = true;
            public bool RollbackSucceeds = true;
            public bool ThrowOnCommit;

            public ISettlementClaimTransactionSnapshot CaptureSnapshot()
            {
                Calls.Add("snapshot");
                return new FakeSnapshot();
            }

            public bool TryStage(SettlementClaimEconomicPlan plan, out string errorCode)
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

            public bool TryRollback(ISettlementClaimTransactionSnapshot snapshot, out string errorCode)
            {
                Calls.Add("rollback");
                errorCode = RollbackSucceeds ? string.Empty : "ROLLBACK_FAILED";
                return RollbackSucceeds;
            }

            public void CommitRuntime(SettlementClaimEconomicPlan plan)
            {
                Calls.Add("commit");
                if (ThrowOnCommit) throw new InvalidOperationException("commit failed");
            }

            public void PublishSuccess(SettlementClaimEconomicPlan plan)
            {
                Calls.Add("event");
            }
        }
    }
}
