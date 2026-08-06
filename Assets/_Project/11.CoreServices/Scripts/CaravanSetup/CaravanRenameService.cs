using System;
using UnityEngine;

namespace ND.Framework
{
    public sealed class CaravanRenameResult
    {
        public bool Succeeded { get; private set; }
        public string Error { get; private set; } = string.Empty;

        public static CaravanRenameResult Success() => new CaravanRenameResult { Succeeded = true };
        public static CaravanRenameResult Failure(string error) =>
            new CaravanRenameResult { Error = error ?? string.Empty };
    }

    /// <summary>Validates and persists one Caravan display name by stable caravanId.</summary>
    public sealed class CaravanRenameService
    {
        public const int MaxLength = 8;
        private readonly Func<SaveData> getSaveData;
        private readonly ISaveService saveService;

        public CaravanRenameService(Func<SaveData> getSaveData, ISaveService saveService)
        {
            this.getSaveData = getSaveData;
            this.saveService = saveService;
        }

        public CaravanRenameResult Execute(string caravanId, string requestedName)
        {
            SaveData saveData = getSaveData?.Invoke();
            string name = requestedName?.Trim() ?? string.Empty;
            if (saveData == null || saveService == null)
                return CaravanRenameResult.Failure("저장 서비스를 사용할 수 없습니다.");
            if (string.IsNullOrWhiteSpace(caravanId)
                || !SaveDataLookup.TryGetCaravan(saveData, caravanId.Trim(), out CaravanSaveData caravan)
                || caravan == null)
                return CaravanRenameResult.Failure("캐러반을 찾을 수 없습니다.");
            if (name.Length == 0 || name.Length > MaxLength)
                return CaravanRenameResult.Failure($"이름은 1~{MaxLength}자로 입력해 주세요.");

            string snapshot = JsonUtility.ToJson(saveData);
            try
            {
                caravan.displayName = name;
                SaveResult result = saveService.Save(saveData);
                if (result != null && result.Succeeded) return CaravanRenameResult.Success();
            }
            catch (Exception exception)
            {
                FrameworkLog.Error($"Caravan rename save failed: {exception.Message}");
            }

            JsonUtility.FromJsonOverwrite(snapshot, saveData);
            return CaravanRenameResult.Failure("캐러반 이름을 저장하지 못했습니다.");
        }
    }
}
