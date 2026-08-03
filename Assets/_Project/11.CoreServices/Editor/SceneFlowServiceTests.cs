using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND.Framework.Editor
{
    public sealed class SceneFlowServiceTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.45f, 0.5f)]
        [TestCase(0.9f, 1f)]
        [TestCase(2f, 1f)]
        public void Progress01_NormalizesUnityPreActivationProgress(float raw, float expected)
        {
            var backendOperation = new FakeBackendOperation { Progress = raw };
            var operation = new UnitySceneLoadOperation("InGame", backendOperation, true);

            Assert.That(operation.Progress01, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void DeferredLoad_RemainsLoadingUntilActivationCompletes()
        {
            var backend = new FakeBackend();
            var service = new SceneFlowService(backend);

            var operation = service.BeginLoadScene("InGame", true);
            backend.Operation.Progress = 0.9f;

            Assert.That(operation, Is.Not.Null);
            Assert.That(operation.IsReadyForActivation, Is.True);
            Assert.That(operation.IsActivationAllowed, Is.False);
            Assert.That(service.IsLoading, Is.True);

            operation.AllowActivation();
            operation.AllowActivation();
            Assert.That(backend.Operation.AllowSceneActivation, Is.True);
            Assert.That(service.IsLoading, Is.True);

            backend.Operation.Complete();
            Assert.That(service.IsLoading, Is.False);
            Assert.That(operation.IsCompleted, Is.True);
        }

        [Test]
        public void CompletionCallback_RaisesSceneChangedOnlyOnce()
        {
            var backend = new FakeBackend();
            var service = new SceneFlowService(backend);
            var raisedCount = 0;
            Action<string> handler = _ => raisedCount++;
            FrameworkEvents.SceneChanged += handler;
            try
            {
                service.BeginLoadScene("InGame", true);
                backend.Operation.Complete();
                backend.Operation.RaiseCompletedAgain();

                Assert.That(raisedCount, Is.EqualTo(1));
            }
            finally
            {
                FrameworkEvents.SceneChanged -= handler;
            }
        }

        [Test]
        public void BeginLoadScene_RejectsDuplicateAndRestoresStateAfterStartFailure()
        {
            var backend = new FakeBackend();
            var service = new SceneFlowService(backend);

            Assert.That(service.BeginLoadScene("InGame", true), Is.Not.Null);
            Assert.That(service.BeginLoadScene("Title"), Is.Null);
            Assert.That(backend.StartCount, Is.EqualTo(1));

            var failedService = new SceneFlowService(new FakeBackend { FailStart = true });
            LogAssert.Expect(LogType.Error, "[Framework] Scene load failed to start: InGame");
            Assert.That(failedService.BeginLoadScene("InGame"), Is.Null);
            Assert.That(failedService.IsLoading, Is.False);
        }

        [Test]
        public void BeginLoadScene_RejectsBlankNameWithoutStartingBackend()
        {
            var backend = new FakeBackend();
            var service = new SceneFlowService(backend);

            Assert.That(service.BeginLoadScene(" "), Is.Null);
            Assert.That(service.IsLoading, Is.False);
            Assert.That(backend.StartCount, Is.Zero);
        }

        private sealed class FakeBackend : ISceneLoadBackend
        {
            public FakeBackendOperation Operation { get; } = new FakeBackendOperation();
            public bool FailStart { get; set; }
            public int StartCount { get; private set; }

            public ISceneLoadBackendOperation LoadSceneAsync(string sceneName)
            {
                StartCount++;
                return FailStart ? null : Operation;
            }
        }

        private sealed class FakeBackendOperation : ISceneLoadBackendOperation
        {
            public float Progress { get; set; }
            public bool AllowSceneActivation { get; set; }
            public bool IsDone { get; private set; }
            public event Action Completed;

            public void Complete()
            {
                if (IsDone)
                    return;

                IsDone = true;
                Completed?.Invoke();
            }

            public void RaiseCompletedAgain()
            {
                Completed?.Invoke();
            }
        }
    }
}
