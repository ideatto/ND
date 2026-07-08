using System;
using System.IO;
using UnityEngine;

namespace ND.Framework
{
    public sealed class JsonSaveService : ISaveService
    {
        private const string FileName = "save_data.json";

        private readonly string savePath;

        public JsonSaveService()
        {
            savePath = Path.Combine(Application.persistentDataPath, FileName);
        }

        public bool HasSaveData()
        {
            return File.Exists(savePath);
        }

        public SaveData CreateNewGameData()
        {
            var data = new SaveData
            {
                version = SaveData.CurrentVersion,
                lastSavedUtcTicks = DateTime.UtcNow.Ticks
            };

            FrameworkLog.Info("New game save data created.");
            return data;
        }

        public SaveData Load()
        {
            if (!HasSaveData())
            {
                FrameworkLog.Warning("Save file was not found. Creating new game data.");
                return CreateNewGameData();
            }

            try
            {
                var json = File.ReadAllText(savePath);
                var data = JsonUtility.FromJson<SaveData>(json);

                if (data == null || data.version != SaveData.CurrentVersion)
                {
                    FrameworkLog.Warning("Save data is invalid or has an unsupported version. Creating new game data.");
                    return CreateNewGameData();
                }

                NormalizeData(data);
                FrameworkLog.Info($"Save data loaded. Version: {data.version}");
                return data;
            }
            catch (Exception exception)
            {
                FrameworkLog.Error($"Failed to load save data: {exception.Message}");
                return CreateNewGameData();
            }
        }

        public void Save(SaveData data)
        {
            if (data == null)
            {
                FrameworkLog.Warning("Save was skipped because data is null.");
                return;
            }

            try
            {
                NormalizeData(data);
                data.version = SaveData.CurrentVersion;
                data.lastSavedUtcTicks = DateTime.UtcNow.Ticks;

                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(savePath, json);
                FrameworkLog.Info($"Save data written: {savePath}");
            }
            catch (Exception exception)
            {
                FrameworkLog.Error($"Failed to save data: {exception.Message}");
            }
        }

        public void ResetSaveData()
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
                FrameworkLog.Info("Save data reset.");
            }
        }

        private static void NormalizeData(SaveData data)
        {
            if (data.player == null)
            {
                data.player = new PlayerSaveData();
            }

            if (data.caravan == null)
            {
                data.caravan = new CaravanSaveData();
            }

            if (data.caravan.inventory == null)
            {
                data.caravan.inventory = new System.Collections.Generic.List<TradeItemBundleSaveData>();
            }

            if (data.tradeProgress == null)
            {
                data.tradeProgress = new TradeProgressSaveData();
            }

            if (data.world == null)
            {
                data.world = new WorldSaveData();
            }

            if (data.tutorial == null)
            {
                data.tutorial = new TutorialSaveData();
            }
        }
    }
}
