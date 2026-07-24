using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class InvestmentQuestCommandTests
    {
        [Test]
        public void Execute_ValidInvestment_CompletesAfterSave()
        {
            var port = new FakePort();

            InvestmentQuestCommandResult result =
                InvestmentQuestCommand.Execute(Input(), port);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailurePhase,
                Is.EqualTo(InvestmentQuestCommandFailurePhase.None));
            Assert.That(result.SaveSucceeded, Is.True);
            Assert.That(result.RuntimeCommitted, Is.True);
            Assert.That(result.SuccessEventPublished, Is.True);
            Assert.That(result.Plan.QuestId, Is.EqualTo("invest-south"));
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "commit", "event" }));
        }

        [Test]
        public void Execute_ValidationFailure_DoesNotCallPersistence()
        {
            var port = new FakePort();
            InvestmentQuestInput input = Input();
            input.IsAlreadyCompleted = true;

            InvestmentQuestCommandResult result =
                InvestmentQuestCommand.Execute(input, port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailurePhase,
                Is.EqualTo(InvestmentQuestCommandFailurePhase.Validation));
            Assert.That(result.ValidationFailureReason,
                Is.EqualTo(InvestmentQuestFailureReason.AlreadyCompleted));
            Assert.That(result.ErrorCode,
                Is.EqualTo("INVESTMENT_ALREADY_COMPLETED"));
            Assert.That(result.Plan, Is.Null);
            Assert.That(port.Calls, Is.Empty);
        }

        [Test]
        public void Execute_SaveFailure_IsSeparatedFromValidation()
        {
            var port = new FakePort { SaveSucceeds = false };

            InvestmentQuestCommandResult result =
                InvestmentQuestCommand.Execute(Input(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailurePhase,
                Is.EqualTo(InvestmentQuestCommandFailurePhase.Persistence));
            Assert.That(result.ValidationFailureReason,
                Is.EqualTo(InvestmentQuestFailureReason.None));
            Assert.That(result.TransactionFailureReason,
                Is.EqualTo(InvestmentQuestTransactionFailureReason.SaveFailed));
            Assert.That(result.ErrorCode, Is.EqualTo("DISK_WRITE_FAILED"));
            Assert.That(result.RuntimeCommitted, Is.False);
            Assert.That(result.SuccessEventPublished, Is.False);
            Assert.That(port.Calls, Is.EqualTo(
                new[] { "snapshot", "stage", "save", "rollback" }));
        }

        [Test]
        public void Execute_MissingPort_ReturnsPersistenceFailure()
        {
            InvestmentQuestCommandResult result =
                InvestmentQuestCommand.Execute(Input(), null);

            Assert.That(result.FailurePhase,
                Is.EqualTo(InvestmentQuestCommandFailurePhase.Persistence));
            Assert.That(result.TransactionFailureReason,
                Is.EqualTo(InvestmentQuestTransactionFailureReason.InvalidInput));
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_INPUT"));
        }

        private static InvestmentQuestInput Input()
        {
            return new InvestmentQuestInput
            {
                RequestedQuestId = "invest-south",
                CaravanId = "caravan-4",
                CanSubmitCaravanAssets = true,
                TradingCurrency = 400,
                Definition = new InvestmentQuestDefinition
                {
                    QuestId = "invest-south",
                    TradingCurrencyCost = 100,
                    ItemCosts =
                    {
                        new InvestmentItemCost
                        {
                            ItemId = "brick",
                            Quantity = 2
                        }
                    },
                    UnlockTownIds = { "town-south" }
                },
                CaravanInventory =
                {
                    new InvestmentInventoryEntry
                    {
                        ItemId = "brick",
                        Quantity = 3
                    }
                }
            };
        }

        private sealed class Snapshot : IInvestmentQuestTransactionSnapshot
        {
        }

        private sealed class FakePort : IInvestmentQuestTransactionPort
        {
            public readonly List<string> Calls = new List<string>();
            public bool SaveSucceeds = true;

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
                IInvestmentQuestTransactionSnapshot snapshot,
                out string errorCode)
            {
                Calls.Add("rollback");
                errorCode = string.Empty;
                return true;
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
