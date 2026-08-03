/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Owns framework scene transitions, normalized progress, and deferred activation.
 *
 * Main Public APIs
 * - BeginLoadScene(...): starts a load and returns a framework-owned operation view.
 * - LoadScene(...), GoToTitle(), GoToLoading(), GoToInGame(): preserve automatic activation.
 *
 * Important Notes
 * - IsLoading remains true until actual activation completes.
 * - Rejected or failed start requests return null; completion raises SceneChanged once.
 */
using System;

namespace ND.Framework
{
    /// <summary>Owns framework scene transitions and publishes completion after activation.</summary>
    public sealed class SceneFlowService
    {
        private readonly ISceneLoadBackend backend;

        public SceneFlowService()
            : this(new UnitySceneLoadBackend())
        {
        }

        internal SceneFlowService(ISceneLoadBackend backend)
        {
            this.backend = backend;
        }

        /// <summary>
        /// Remains true from a successful load start through deferred activation and actual completion.
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>Starts an automatically activated scene load for compatibility.</summary>
        public void LoadScene(string sceneName)
        {
            BeginLoadScene(sceneName);
        }

        /// <summary>
        /// Starts one scene load and optionally holds activation after normalized progress reaches 1.
        /// </summary>
        /// <returns>
        /// A live operation view when loading starts; otherwise null. Rejection does not alter an
        /// existing load, and start failure restores <see cref="IsLoading"/> immediately.
        /// </returns>
        public ISceneLoadOperation BeginLoadScene(string sceneName, bool deferActivation = false)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                FrameworkLog.Warning("Scene load was skipped because scene name is empty.");
                return null;
            }

            if (IsLoading)
            {
                FrameworkLog.Warning($"Scene load ignored while another load is running: {sceneName}");
                return null;
            }

            try
            {
                IsLoading = true;
                FrameworkLog.Info($"Loading scene: {sceneName}");
                var backendOperation = backend.LoadSceneAsync(sceneName);
                if (backendOperation == null)
                {
                    IsLoading = false;
                    FrameworkLog.Error($"Scene load failed to start: {sceneName}");
                    return null;
                }

                var loadOperation = new UnitySceneLoadOperation(
                    sceneName, backendOperation, deferActivation);
                var completionHandled = false;
                loadOperation.Completed += () =>
                {
                    if (completionHandled)
                        return;

                    completionHandled = true;
                    IsLoading = false;
                    FrameworkEvents.RaiseSceneChanged(sceneName);
                };
                return loadOperation;
            }
            catch (Exception exception)
            {
                IsLoading = false;
                FrameworkLog.Error($"Scene load failed: {sceneName}, {exception.Message}");
                return null;
            }
        }

        public void GoToTitle() => LoadScene(SceneNames.Title);
        public void GoToLoading() => LoadScene(SceneNames.Loading);
        public void GoToInGame() => LoadScene(SceneNames.InGame);
    }
}
