using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class DualGrowthTransactionContractTests
    {
        [Test]
        public void Execute_Success_UsesRequiredOrdering()
        {
            var port = new FakePort();

            DualGrowthTransactionResult result =
                DualGrowthTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SaveSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.True);
            Assert.That(result.SuccessEventPublished, Is.True);
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "commit", "event" }));
        }

        [Test]
        public void Execute_SaveFailure_RollsBackCurrencyAndLevels()
        {
            var port = new FakePort { SaveSucceeds = false };

            DualGrowthTransactionResult result =
                DualGrowthTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthTransactionFailureReason.SaveFailed));
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.False);
            Assert.That(result.SuccessEventPublished, Is.False);
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "rollback" }));
        }

        [Test]
        public void Execute_StageFailure_DoesNotAttemptSave()
        {
            var port = new FakePort { StageSucceeds = false };

            DualGrowthTransactionResult result =
                DualGrowthTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthTransactionFailureReason.StageFailed));
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

            DualGrowthTransactionResult result =
                DualGrowthTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthTransactionFailureReason.RollbackFailed));
            Assert.That(result.ErrorCode, Is.EqualTo("ROLLBACK_FAILED"));
        }

        [Test]
        public void Execute_NullPlan_DoesNotCallPort()
        {
            var port = new FakePort();

            DualGrowthTransactionResult result =
                DualGrowthTransactionExecutor.Execute(null, port);

            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthTransactionFailureReason.InvalidInput));
            Assert.That(port.Calls, Is.Empty);
        }

        private static DualGrowthEconomicPlan Plan()
        {
            return new DualGrowthEconomicPlan(
                "player_capacity",
                GrowthAxis.Player,
                1,
                2,
                1,
                2,
                3,
                3,
                100,
                20,
                80,
                new[] { new GrowthEffectPlan("player_capacity", 10) });
        }

        private sealed class Snapshot : IDualGrowthTransactionSnapshot
        {
        }

        private sealed class FakePort : IDualGrowthTransactionPort
        {
            public readonly List<string> Calls = new List<string>();
            public bool StageSucceeds = true;
            public bool SaveSucceeds = true;
            public bool RollbackSucceeds = true;

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
                IDualGrowthTransactionSnapshot snapshot,
                out string errorCode)
            {
                Calls.Add("rollback");
                errorCode = RollbackSucceeds ? string.Empty : "ROLLBACK_FAILED";
                return RollbackSucceeds;
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
