using UnityEngine;

namespace ND.Framework
{
    public sealed class FrameworkRoot : MonoBehaviour
    {
        private const string RootObjectName = "FrameworkRoot";

        public static FrameworkRoot Instance { get; private set; }

        public GameTimeService GameTime { get; private set; }
        public ISaveService SaveService { get; private set; }
        public SceneFlowService SceneFlow { get; private set; }
        public FrameworkDebugCommands DebugCommands { get; private set; }
        public SaveData CurrentSaveData { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRootExists()
        {
            if (Instance != null)
            {
                return;
            }

            var rootObject = new GameObject(RootObjectName);
            rootObject.AddComponent<FrameworkRoot>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                FrameworkLog.Warning("Duplicate FrameworkRoot destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeServices();
        }

        public void StartNewGame()
        {
            CurrentSaveData = SaveService.CreateNewGameData();
            SaveService.Save(CurrentSaveData);
            SceneFlow.GoToLoading();
        }

        public void ContinueGame()
        {
            CurrentSaveData = SaveService.Load();
            SceneFlow.GoToLoading();
        }

        public void CompleteLoadingAndEnterGame()
        {
            if (CurrentSaveData == null)
            {
                CurrentSaveData = SaveService.Load();
            }

            FrameworkEvents.RaiseLoadCompleted(CurrentSaveData);
            SceneFlow.GoToInGame();
        }

        public void ReturnToTitle()
        {
            if (CurrentSaveData != null)
            {
                SaveService.Save(CurrentSaveData);
            }

            SceneFlow.GoToTitle();
        }

        private void InitializeServices()
        {
            GameTime = new GameTimeService();
            SaveService = new JsonSaveService();
            SceneFlow = new SceneFlowService();
            DebugCommands = new FrameworkDebugCommands(GameTime);
            CurrentSaveData = SaveService.HasSaveData() ? SaveService.Load() : SaveService.CreateNewGameData();

            FrameworkLog.Info("FrameworkRoot initialized.");
        }
    }
}
