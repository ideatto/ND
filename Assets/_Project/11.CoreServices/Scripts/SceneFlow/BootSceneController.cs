using UnityEngine;

namespace ND.Framework
{
    public sealed class BootSceneController : MonoBehaviour
    {
        [SerializeField] private bool loadTitleOnStart = true;

        private void Start()
        {
            if (!loadTitleOnStart)
            {
                return;
            }

            FrameworkRoot.Instance.SceneFlow.GoToTitle();
        }
    }
}
