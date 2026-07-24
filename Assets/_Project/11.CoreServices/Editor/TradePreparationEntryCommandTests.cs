#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ND.Framework.Editor
{
    [InitializeOnLoad]
    public sealed class TradePreparationEntryCommandTests
    {
        private const string ProbeSessionKey = "ND.TradePreparationEntryCommandTests.20260722.v1";

        static TradePreparationEntryCommandTests()
        {
            if (SessionState.GetBool(ProbeSessionKey, false))
                return;

            SessionState.SetBool(ProbeSessionKey, true);
            EditorApplication.delayCall += RunProbe;
        }

        private static void RunProbe()
        {
            try
            {
                var tests = new TradePreparationEntryCommandTests();
                tests.TownEntry_ResetsProgressSavesAndRoutesToPreparation();
                tests.NonTownEntry_IsRejectedWithoutMutation();
                tests.SaveFailure_RestoresPreviousProgress();
                Debug.Log("Trade preparation entry probe passed (3/3).");
            }
            catch (Exception exception)
            {
                Debug.LogError("Trade preparation entry probe failed: " + exception);
            }
        }

        [Test]
        public void TownEntry_ResetsProgressSavesAndRoutesToPreparation()
        {
            SaveData save = CreateTownSave();
            var service = new ProbeSaveService();
            var router = new InGameScreenStateRouter();

            bool result = TradePreparationEntryCommand.TryExecute(save, service, router);

            Assert.That(result, Is.True);
            Assert.That(service.SaveCount, Is.EqualTo(1));
            Assert.That(save.tradeProgress.state, Is.EqualTo(TradeProgressState.Preparing));
            Assert.That(save.tradeProgress.activeTradeId, Is.Empty);
            Assert.That(save.tradeProgress.activeRouteId, Is.Empty);
            Assert.That(save.tradeProgress.tradeStartUtcTick, Is.Zero);
            Assert.That(save.tradeProgress.expectedTradeEndUtcTick, Is.Zero);
            Assert.That(save.caravan.elapsedInGameSeconds, Is.Zero);
            Assert.That(router.CurrentScreenState, Is.EqualTo(InGameScreenState.Preparation));
        }

        [Test]
        public void NonTownEntry_IsRejectedWithoutMutation()
        {
            SaveData save = CreateTownSave();
            save.tradeProgress.state = TradeProgressState.Traveling;
            string before = JsonUtility.ToJson(save);
            var service = new ProbeSaveService();

            bool result = TradePreparationEntryCommand.TryExecute(
                save,
                service,
                new InGameScreenStateRouter());

            Assert.That(result, Is.False);
            Assert.That(service.SaveCount, Is.Zero);
            Assert.That(JsonUtility.ToJson(save), Is.EqualTo(before));
        }

        [Test]
        public void SaveFailure_RestoresPreviousProgress()
        {
            SaveData save = CreateTownSave();
            string before = JsonUtility.ToJson(save);
            var service = new ProbeSaveService { FailSave = true };

            bool result = TradePreparationEntryCommand.TryExecute(
                save,
                service,
                new InGameScreenStateRouter());

            Assert.That(result, Is.False);
            Assert.That(service.SaveCount, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(save), Is.EqualTo(before));
        }

        private static SaveData CreateTownSave()
        {
            var save = new SaveData();
            save.player.currentTownId = "river-town";
            var progress = new TradeProgressSaveData
            {
                caravanId = save.selectedCaravanId
            };
            save.tradeProgressEntries.Add(progress);
            progress.state = TradeProgressState.Completed;
            progress.activeTradeId = "completed-trade";
            progress.activeRouteId = "river-route";
            progress.tradeStartUtcTick = 10L;
            progress.expectedTradeEndUtcTick = 20L;
            save.caravans[0].elapsedInGameSeconds = 15f;
            return save;
        }

        private sealed class ProbeSaveService : ISaveService
        {
            public bool FailSave { get; set; }
            public int SaveCount { get; private set; }

            public bool HasSaveData() => false;
            public SaveData CreateNewGameData() => new SaveData();
            public SaveData Load() => null;

            public SaveResult Save(SaveData data)
            {
                SaveCount++;
                return FailSave
                    ? SaveResult.Failure(SaveFailureReason.WriteFailed, "Forced failure.", "entry-probe")
                    : SaveResult.Success();
            }

            public void ResetSaveData() { }
        }
    }
}
#endif
