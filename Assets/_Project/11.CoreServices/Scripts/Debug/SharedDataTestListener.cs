

namespace ND.Framework
{
    using UnityEngine;

    public sealed class SharedDataTestListener : MonoBehaviour
    {
        private void OnEnable()
        {
            FrameworkEvents.SharedGameDataLoaded += OnSharedGameDataLoaded;
            FrameworkEvents.LoadCompleted += OnLoadCompleted;
        }

        private void OnDisable()
        {
            FrameworkEvents.SharedGameDataLoaded -= OnSharedGameDataLoaded;
            FrameworkEvents.LoadCompleted -= OnLoadCompleted;
        }

        private void OnSharedGameDataLoaded(ISharedGameDataProvider provider)
        {
            Debug.Log($"[SharedDataTest] SharedGameDataLoaded: {provider.Summary}");
        }

        private void OnLoadCompleted(SaveData saveData)
        {
            Debug.Log("[SharedDataTest] LoadCompleted");
        }
    }
}