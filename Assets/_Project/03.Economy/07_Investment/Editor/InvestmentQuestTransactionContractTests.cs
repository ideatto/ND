using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class InvestmentQuestTransactionContractTests
    {
        [Test]
        public void Execute_Success_UsesRequiredOrdering()
        {
            var port = new FakePort();

            InvestmentQuestTransactionResult result =
                InvestmentQuestTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SaveSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.True);
            Assert.That(result.SuccessEventPublished, Is.True);
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "commit", "event" }));
        }

        [Test]
        public void Execute_SaveFailure_RollsBackAllInvestmentState()
        {
            var port = new FakePort { SaveSucceeds = false };

            InvestmentQuestTransactionResult result =
                InvestmentQuestTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestTransactionFailureReason.SaveFailed));
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.False);
            Assert.That(result.SuccessEventPublished, Is.False);
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "rollback" }));
        }

        [Test]
        public void Execute_StageFailure_DoesNotSave()
        {
            var port = new FakePort { StageSucceeds = false };

            InvestmentQuestTransactionResult result =
                InvestmentQuestTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestTransactionFailureReason.StageFailed));
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "rollback" }));
        }

        [Test]
        public void Execute_RollbackFailure_IsReportedSeparately()
        {
            var port = new FakePort
            {
                SaveSucceeds = false,
                RollbackSucceeds = false
            };

            InvestmentQuestTransactionResult result =
                InvestmentQuestTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestTransactionFailureReason.RollbackFailed));
            Assert.That(result.ErrorCode, Is.EqualTo("ROLLBACK_FAILED"));
        }

        [Test]
        public void Execute_NullPlan_DoesNotCallPort()
        {
            var port = new FakePort();

            InvestmentQuestTransactionResult result =
                InvestmentQuestTransactionExecutor.Execute(null, port);

            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestTransactionFailureReason.InvalidInput));
            Assert.That(port.Calls, Is.Empty);
        }

        private static InvestmentQuestEconomicPlan Plan()
        {
            return new InvestmentQuestEconomicPlan(
                "invest-east",
                "caravan-3",
                500,
                150,
                350,
                new[] { new InvestmentItemPlan("stone", 3, 7, 4) },
                new[] { "town-east" },
                new[] { "route-east" });
        }

        private sealed class Snapshot : IInvestmentQuestTransactionSnapshot
        {
        }

        private sealed class FakePort : IInvestmentQuestTransactionPort
        {
            public readonly List<string> Calls = new List<string>();
            public bool StageSucceeds = true;
            public bool SaveSucceeds = true;
            public bool RollbackSucceeds = true;

            public IInvestmentQuestTransactionSnapshot CaptureSnapshot()
            {
                Calls.Add("snapshot");
                return new Snapshot();
            }

            public bool TryStage(
                InvestmentQuestEconomicPlan plan,
                out string errorCode)
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

            public bool TryRollback(
                IInvestmentQuestTransactionSnapshot snapshot,
                out string errorCode)
            {
                Calls.Add("rollback");
                errorCode = RollbackSucceeds ? string.Empty : "ROLLBACK_FAILED";
                return RollbackSucceeds;
            }

            public void CommitRuntime(InvestmentQuestEconomicPlan plan)
            {
                Calls.Add("commit");
            }

            public void PublishSuccess(InvestmentQuestEconomicPlan plan)
            {
                Calls.Add("event");
            }
        }
    }
}
