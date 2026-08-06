using System;
using ND.Framework;

namespace ND.UI.InGame.TransportInventory
{
    /// <summary>Framework-independent adapter that composes SaveData with SharedGameData.</summary>
    public sealed class TransportInventoryDataProvider : ITransportInventoryViewDataProvider
    {
        private readonly Func<ND.Framework.SaveData> getSaveData;
        private readonly Func<ISharedGameDataProvider> getSharedGameData;

        public TransportInventoryDataProvider(
            Func<ND.Framework.SaveData> getSaveData,
            Func<ISharedGameDataProvider> getSharedGameData)
        {
            this.getSaveData = getSaveData ?? throw new ArgumentNullException(nameof(getSaveData));
            this.getSharedGameData = getSharedGameData ?? throw new ArgumentNullException(nameof(getSharedGameData));
        }

        public TransportInventoryPopupViewData GetViewData()
        {
            return TransportInventoryViewDataBuilder.Build(getSaveData(), getSharedGameData());
        }
    }
}
