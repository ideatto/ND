using System;
using UnityEngine;

namespace ND.UI.Tutorial
{
    /// <summary>Grants exclusive access to the shared tutorial log and dialogue presentation.</summary>
    public sealed class TutorialPresentationCoordinator : MonoBehaviour
    {
        private UnityEngine.Object owner;

        public bool IsAvailable => owner == null;
        public UnityEngine.Object Owner => owner;

        public event Action AvailabilityChanged;

        public bool TryAcquire(UnityEngine.Object requester)
        {
            if (requester == null)
                return false;
            if (owner != null && owner != requester)
                return false;
            if (owner == requester)
                return true;

            owner = requester;
            AvailabilityChanged?.Invoke();
            return true;
        }

        public bool IsOwnedBy(UnityEngine.Object requester) =>
            requester != null && owner == requester;

        public void Release(UnityEngine.Object requester)
        {
            if (!IsOwnedBy(requester))
                return;

            owner = null;
            AvailabilityChanged?.Invoke();
        }
    }
}
