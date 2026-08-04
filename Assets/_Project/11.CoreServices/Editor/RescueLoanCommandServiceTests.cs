#if UNITY_EDITOR
using ND.Economy;
using NUnit.Framework;

namespace ND.Framework.Editor
{
    public sealed class RescueLoanCommandServiceTests
    {
        [Test]
        public void IssueRescueLoan_RaisesChangedTradingCurrencyAfterSuccessfulSave()
        {
            SaveData data = new SaveData();
            data.player.tradingCurrency = 652L;
            RescueLoanCommandService service = CreateService(data);
            long? changedCurrency = null;
            void HandleChanged(long value) => changedCurrency = value;

            FrameworkEvents.TradingCurrencyChanged += HandleChanged;
            try
            {
                SaveResult result = service.IssueRescueLoan();

                Assert.That(result.Succeeded, Is.True);
                Assert.That(data.player.tradingCurrency, Is.EqualTo(1652L));
                Assert.That(changedCurrency, Is.EqualTo(1652L));
            }
            finally
            {
                FrameworkEvents.TradingCurrencyChanged -= HandleChanged;
            }
        }

        [Test]
        public void RepayRescueLoan_RaisesChangedTradingCurrencyAfterSuccessfulSave()
        {
            SaveData data = new SaveData();
            data.player.tradingCurrency = 1652L;
            data.rescueLoan.loanId = "rescue_loan";
            data.rescueLoan.originalPrincipal = 1000L;
            data.rescueLoan.remainingPrincipal = 1000L;
            data.rescueLoan.isActive = true;
            data.rescueLoan.isRestrictedPreparation = true;
            RescueLoanCommandService service = CreateService(data);
            long? changedCurrency = null;
            void HandleChanged(long value) => changedCurrency = value;

            FrameworkEvents.TradingCurrencyChanged += HandleChanged;
            try
            {
                SaveResult result = service.RepayRescueLoan(100L);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(data.player.tradingCurrency, Is.EqualTo(1552L));
                Assert.That(changedCurrency, Is.EqualTo(1552L));
            }
            finally
            {
                FrameworkEvents.TradingCurrencyChanged -= HandleChanged;
            }
        }

        private static RescueLoanCommandService CreateService(SaveData data) =>
            new RescueLoanCommandService(
                new SuccessfulSaveService(),
                () => data,
                new RescueLoanDefinition
                {
                    LoanId = "rescue_loan",
                    MinimumTradeCost = 1000L
                },
                () => 123L);

        private sealed class SuccessfulSaveService : ISaveService
        {
            public bool HasSaveData() => false;
            public SaveData CreateNewGameData() => new SaveData();
            public SaveData Load() => null;
            public SaveResult Save(SaveData data) => SaveResult.Success();
            public void ResetSaveData() { }
        }
    }
}
#endif
