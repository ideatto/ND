using System;
using UnityEngine.SceneManagement;

namespace ND.Framework
{
    public sealed class SceneFlowService
    {
        public bool IsLoading { get; private set; }

        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                FrameworkLog.Warning("Scene load was skipped because scene name is empty.");
                return;
            }

            if (IsLoading)
            {
                FrameworkLog.Warning($"Scene load ignored while another load is running: {sceneName}");
                return;
            }

            try
            {
                IsLoading = true;
                FrameworkLog.Info($"Loading scene: {sceneName}");

                var operation = SceneManager.LoadSceneAsync(sceneName);
                if (operation == null)
                {
                    IsLoading = false;
                    FrameworkLog.Error($"Scene load failed to start: {sceneName}");
                    return;
                }

                operation.completed += _ =>
                {
                    IsLoading = false;
                    FrameworkEvents.RaiseSceneChanged(sceneName);
                };
            }
            catch (Exception exception)
            {
                IsLoading = false;
                FrameworkLog.Error($"Scene load failed: {sceneName}, {exception.Message}");
            }
        }

        public void GoToTitle()
        {
            LoadScene(SceneNames.Title);
        }

        public void GoToLoading()
        {
            LoadScene(SceneNames.Loading);
        }

        public void GoToInGame()
        {
            LoadScene(SceneNames.InGame);
        }
    }
}
