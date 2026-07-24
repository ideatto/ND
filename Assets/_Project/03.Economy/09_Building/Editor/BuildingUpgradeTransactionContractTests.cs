using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class BuildingUpgradeTransactionContractTests
    {
        [Test]
        public void Execute_Success_UsesRequiredOrdering()
        {
            var port = new FakePort();

            BuildingUpgradeTransactionResult result =
                BuildingUpgradeTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SaveSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.True);
            Assert.That(result.SuccessEventPublished, Is.True);
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "commit", "event" }));
        }

        [Test]
        public void Execute_SaveFailure_RollsBackMaterialsAndLevel()
        {
            var port = new FakePort { SaveSucceeds = false };

            BuildingUpgradeTransactionResult result =
                BuildingUpgradeTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeTransactionFailureReason.SaveFailed));
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

            BuildingUpgradeTransactionResult result =
                BuildingUpgradeTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeTransactionFailureReason.StageFailed));
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

            BuildingUpgradeTransactionResult result =
                BuildingUpgradeTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeTransactionFailureReason.RollbackFailed));
            Assert.That(result.ErrorCode, Is.EqualTo("ROLLBACK_FAILED"));
        }

        [Test]
        public void Execute_NullPlan_DoesNotCallPort()
        {
            var port = new FakePort();

            BuildingUpgradeTransactionResult result =
                BuildingUpgradeTransactionExecutor.Execute(null, port);

            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeTransactionFailureReason.InvalidInput));
            Assert.That(port.Calls, Is.Empty);
        }

        private static BuildingUpgradeEconomicPlan Plan()
        {
            return new BuildingUpgradeEconomicPlan(
                "warehouse",
                0,
                1,
                new[] { new BuildingUpgradeMaterialPlan("wood", 3, 5, 2) },
                new[] { new BuildingUpgradeEffectPlan("storage_slots", 10) });
        }

        private sealed class Snapshot : IBuildingUpgradeTransactionSnapshot
        {
        }

        private sealed class FakePort : IBuildingUpgradeTransactionPort
        {
            public readonly List<string> Calls = new List<string>();
            public bool StageSucceeds = true;
            public bool SaveSucceeds = true;
            public bool RollbackSucceeds = true;

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
                IBuildingUpgradeTransactionSnapshot snapshot,
                out string errorCode)
            {
                Calls.Add("rollback");
                errorCode = RollbackSucceeds ? string.Empty : "ROLLBACK_FAILED";
                return RollbackSucceeds;
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
