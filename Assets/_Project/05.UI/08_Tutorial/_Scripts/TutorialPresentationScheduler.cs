using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Tutorial
{
    public sealed class TutorialPresentationScheduler : MonoBehaviour
    {
        private sealed class Request
        {
            public UnityEngine.Object Owner;
            public Action Granted;
        }

        [SerializeField] private TutorialPresentationCoordinator coordinator;

        private readonly List<Request> pending = new List<Request>();
        private UnityEngine.Object activeOwner;

        private void OnEnable()
        {
            if (coordinator != null)
            {
                coordinator.AvailabilityChanged -= HandleAvailabilityChanged;
                coordinator.AvailabilityChanged += HandleAvailabilityChanged;
            }
            TryGrantNext();
        }

        private void OnDisable()
        {
            if (coordinator != null)
                coordinator.AvailabilityChanged -= HandleAvailabilityChanged;
            pending.Clear();
            activeOwner = null;
        }

        public void Initialize(TutorialPresentationCoordinator value)
        {
            if (coordinator == value) return;
            if (coordinator != null)
                coordinator.AvailabilityChanged -= HandleAvailabilityChanged;

            coordinator = value;
            if (isActiveAndEnabled && coordinator != null)
            {
                coordinator.AvailabilityChanged -= HandleAvailabilityChanged;
                coordinator.AvailabilityChanged += HandleAvailabilityChanged;
            }
            TryGrantNext();
        }

        public void RequestPresentation(UnityEngine.Object owner, Action granted)
        {
            if (owner == null || IsRequested(owner)) return;
            pending.Add(new Request { Owner = owner, Granted = granted });
            TryGrantNext();
        }

        public void Release(UnityEngine.Object owner)
        {
            Cancel(owner);
            if (activeOwner != owner) return;

            activeOwner = null;
            coordinator?.Release(owner);
            TryGrantNext();
        }

        public void Cancel(UnityEngine.Object owner)
        {
            for (int index = pending.Count - 1; index >= 0; index--)
            {
                if (pending[index].Owner == owner)
                    pending.RemoveAt(index);
            }
        }

        public bool IsActive(UnityEngine.Object owner) => activeOwner == owner;

        private bool IsRequested(UnityEngine.Object owner)
        {
            if (activeOwner == owner) return true;
            for (int index = 0; index < pending.Count; index++)
                if (pending[index].Owner == owner) return true;
            return false;
        }

        private void HandleAvailabilityChanged()
        {
            if (activeOwner == null) TryGrantNext();
        }

        private void TryGrantNext()
        {
            if (!isActiveAndEnabled || activeOwner != null || pending.Count == 0) return;

            Request request = pending[0];
            if (request.Owner == null)
            {
                pending.RemoveAt(0);
                TryGrantNext();
                return;
            }

            pending.RemoveAt(0);
            activeOwner = request.Owner;
            if (coordinator != null && !coordinator.TryAcquire(request.Owner))
            {
                activeOwner = null;
                pending.Insert(0, request);
                return;
            }
            request.Granted?.Invoke();
        }
    }
}
