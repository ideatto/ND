using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ND.Framework
{
    internal interface ISceneLoadBackendOperation
    {
        float Progress { get; }
        bool AllowSceneActivation { get; set; }
        bool IsDone { get; }
        event Action Completed;
    }

    internal interface ISceneLoadBackend
    {
        ISceneLoadBackendOperation LoadSceneAsync(string sceneName);
    }

    internal sealed class UnitySceneLoadBackend : ISceneLoadBackend
    {
        public ISceneLoadBackendOperation LoadSceneAsync(string sceneName)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName);
            return operation == null ? null : new UnityAsyncOperationAdapter(operation);
        }
    }

    internal sealed class UnityAsyncOperationAdapter : ISceneLoadBackendOperation
    {
        private readonly AsyncOperation operation;

        public UnityAsyncOperationAdapter(AsyncOperation operation)
        {
            this.operation = operation;
            operation.completed += _ => Completed?.Invoke();
        }

        public float Progress => operation.progress;
        public bool AllowSceneActivation
        {
            get => operation.allowSceneActivation;
            set => operation.allowSceneActivation = value;
        }
        public bool IsDone => operation.isDone;
        public event Action Completed;
    }

    internal sealed class UnitySceneLoadOperation : ISceneLoadOperation
    {
        private readonly ISceneLoadBackendOperation operation;
        private bool activationAllowed;

        public UnitySceneLoadOperation(
            string sceneName,
            ISceneLoadBackendOperation operation,
            bool deferActivation)
        {
            SceneName = sceneName;
            this.operation = operation;
            activationAllowed = !deferActivation;
            operation.AllowSceneActivation = activationAllowed;
        }

        public string SceneName { get; }
        public float Progress01 => Mathf.Clamp01(operation.Progress / 0.9f);
        public bool IsReadyForActivation => Progress01 >= 1f;
        public bool IsActivationAllowed => activationAllowed;
        public bool IsCompleted => operation.IsDone;
        internal event Action Completed
        {
            add => operation.Completed += value;
            remove => operation.Completed -= value;
        }

        public void AllowActivation()
        {
            if (activationAllowed)
                return;

            activationAllowed = true;
            operation.AllowSceneActivation = true;
        }
    }
}
