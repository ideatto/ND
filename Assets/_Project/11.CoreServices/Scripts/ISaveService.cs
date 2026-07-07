namespace ND.Framework
{
    public interface ISaveService
    {
        bool HasSaveData();
        SaveData CreateNewGameData();
        SaveData Load();
        void Save(SaveData data);
        void ResetSaveData();
    }
}
