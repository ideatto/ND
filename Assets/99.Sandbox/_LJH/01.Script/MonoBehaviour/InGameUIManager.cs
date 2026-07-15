using UnityEngine;

public sealed class InGameUIManager : MonoBehaviour
{
    [SerializeField] private GameObject tradePrepareUI;

    public void ActivePrepareUI()
    {
        if(!ValidateReference(tradePrepareUI, nameof(tradePrepareUI)))
        {
            return;
        }

        if (tradePrepareUI.activeSelf)
        {
            return;
        }

        tradePrepareUI.SetActive(true);
    }

    public void DeactivePrepareUI()
    {
        if (!ValidateReference(tradePrepareUI, nameof(tradePrepareUI)))
        {
            return;
        }

        if (!tradePrepareUI.activeSelf)
        {
            return;
        }

        tradePrepareUI.SetActive(false);
    }

    private bool ValidateReference(GameObject obj, string fieldName)
    {
        if(obj != null)
        {
            return true;
        }

        Debug.LogError($"[InGameUIManager] {fieldName} is not assigned", this);
        return false;
    }
}
