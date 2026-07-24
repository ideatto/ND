using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class CaravanCreationTransactionContractTests
    {
        [Test]
        public void Execute_Success_UsesRequiredOrdering()
        {
            var port = new FakePort();

            CaravanCreationTransactionResult result =
                CaravanCreationTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SaveSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.True);
            Assert.That(result.SuccessEventPublished, Is.True);
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "commit", "event" }));
        }

        [Test]
        public void Execute_SaveFailure_RollsBackCurrencySlotAndCaravan()
        {
            var port = new FakePort { SaveSucceeds = false };

            CaravanCreationTransactionResult result =
                CaravanCreationTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(
                    CaravanCreationTransactionFailureReason.SaveFailed));
            Assert.That(result.RollbackAttempted, Is.True);
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

            CaravanCreationTransactionResult result =
                CaravanCreationTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    CaravanCreationTransactionFailureReason.StageFailed));
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "rollback" }));
        }

        [Test]
        public void Execute_RollbackFailure_OverridesOriginalFailure()
        {
            var port = new FakePort
            {
                SaveSucceeds = false,
                RollbackSucceeds = false
            };

            CaravanCreationTransactionResult result =
                CaravanCreationTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    CaravanCreationTransactionFailureReason.RollbackFailed));
            Assert.That(result.ErrorCode, Is.EqualTo("ROLLBACK_FAILED"));
        }

        [Test]
        public void Execute_NullPlan_DoesNotCallPort()
        {
            var port = new FakePort();

            CaravanCreationTransactionResult result =
                CaravanCreationTransactionExecutor.Execute(null, port);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    CaravanCreationTransactionFailureReason.InvalidInput));
            Assert.That(port.Calls, Is.Empty);
        }

        private static CaravanCreationEconomicPlan Plan()
        {
            return new CaravanCreationEconomicPlan(
                "caravan-new",
                "town-home",
                1,
                2,
                1,
                2,
                1000,
                100,
                900);
        }

        private sealed class Snapshot :
            ICaravanCreationTransactionSnapshot
        {
        }

        private sealed class FakePort : ICaravanCreationTransactionPort
        {
            public readonly List<string> Calls = new List<string>();
            public bool StageSucceeds = true;
            public bool SaveSucceeds = true;
            public bool RollbackSucceeds = true;

            public ICaravanCreationTransactionSnapshot CaptureSnapshot()
            {
                Calls.Add("snapshot");
                return new Snapshot();
            }

            public bool TryStage(
                CaravanCreationEconomicPlan plan,
                out string errorCode)
            {
                Calls.Add("stage");
                errorCode = StageSucceeds
                    ? string.Empty
                    : "STAGE_FAILED";
                return StageSucceeds;
            }

            public bool TrySave(out string errorCode)
            {
                Calls.Add("save");
                errorCode = SaveSucceeds ? string.Empty : "SAVE_FAILED";
                return SaveSucceeds;
            }

            public bool TryRollback(
                ICaravanCreationTransactionSnapshot snapshot,
                out string errorCode)
            {
                Calls.Add("rollback");
                errorCode = RollbackSucceeds
                    ? string.Empty
                    : "ROLLBACK_FAILED";
                return RollbackSucceeds;
            }

            public void CommitRuntime(CaravanCreationEconomicPlan plan)
            {
                Calls.Add("commit");
            }

            public void PublishSuccess(CaravanCreationEconomicPlan plan)
            {
                Calls.Add("event");
            }
        }
    }
}
