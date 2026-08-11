#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 정적 엔딩 Popup Prefab과 Entry를 실제 MainUICanvas Prefab에 연결한다.
/// Player 런타임에는 오브젝트를 생성하지 않으며 반복 실행해도 중복을 남기지 않는다.
/// </summary>
public static class EndingCompletionPopupInstaller
{
    private const string MainUiPath = "Assets/_Project/08.Prefabs/MainUICanvas.prefab";
    private const string PopupPath = "Assets/_Project/08.Prefabs/UI/Building/EndingCompletionPopup.prefab";
    private const string EndingBuildDataPath =
        "Assets/_Project/02.Data/01_ScriptableObjects/Build/Build_EndingItem.asset";

    [MenuItem("ND/UI/Install Ending Completion Into Main UI")]
    public static void Install()
    {
        RepairPopupPrefab();

        GameObject popupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
        if (popupPrefab == null)
            throw new MissingReferenceException($"Ending Popup Prefab을 찾을 수 없습니다: {PopupPath}");

        BuildData endingBuildData = AssetDatabase.LoadAssetAtPath<BuildData>(EndingBuildDataPath);
        if (endingBuildData == null)
            throw new MissingReferenceException($"Ending BuildData를 찾을 수 없습니다: {EndingBuildDataPath}");

        GameObject root = PrefabUtility.LoadPrefabContents(MainUiPath);
        try
        {
            BuildingListPanel buildingList = root.GetComponentInChildren<BuildingListPanel>(true);
            if (buildingList == null)
                throw new MissingComponentException("MainUICanvas에 BuildingListPanel이 없습니다.");

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)
                         .Where(item => item != root.transform && item.name == "EndingCompletionPopup")
                         .ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }

            GameObject popupObject = PrefabUtility.InstantiatePrefab(popupPrefab, root.transform) as GameObject;
            if (popupObject == null)
                throw new MissingReferenceException("EndingCompletionPopup 인스턴스 생성에 실패했습니다.");

            popupObject.name = "EndingCompletionPopup";
            popupObject.SetActive(false);
            EndingCompletionPopupPresenter presenter =
                popupObject.GetComponent<EndingCompletionPopupPresenter>();
            if (presenter == null)
                throw new MissingComponentException("EndingCompletionPopupPresenter가 없습니다.");

            EndingCompletionMainUiEntry[] entries =
                root.GetComponentsInChildren<EndingCompletionMainUiEntry>(true);
            EndingCompletionMainUiEntry entry = entries.FirstOrDefault();
            if (entry == null)
                entry = root.AddComponent<EndingCompletionMainUiEntry>();

            foreach (EndingCompletionMainUiEntry duplicate in entries.Skip(1).ToArray())
                Object.DestroyImmediate(duplicate);

            entry.Configure(buildingList, presenter, endingBuildData);
            EditorUtility.SetDirty(entry);
            PrefabUtility.SaveAsPrefabAsset(root, MainUiPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[EndingCompletion] MainUICanvas 조립 완료.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 프리팹에 실제 Backdrop Button을 저장하고 View에 직렬화한다.
    /// 런타임 생성이나 GetComponent 보정에 의존하지 않도록 조립 단계에서만 실행한다.
    /// </summary>
    private static void RepairPopupPrefab()
    {
        GameObject popupRoot = PrefabUtility.LoadPrefabContents(PopupPath);
        try
        {
            Transform backdropTransform = popupRoot.transform.Find("Backdrop");
            EndingCompletionPopupView view = popupRoot.GetComponent<EndingCompletionPopupView>();
            if (backdropTransform == null || view == null)
                throw new MissingReferenceException("Ending Popup의 Backdrop 또는 View가 없습니다.");

            Button backdrop = backdropTransform.GetComponent<Button>();
            if (backdrop == null)
                backdrop = backdropTransform.gameObject.AddComponent<Button>();
            backdrop.targetGraphic = backdropTransform.GetComponent<Graphic>();
            backdrop.transition = Selectable.Transition.None;

            Button endButton = popupRoot.transform.Find("Panel/EndGameButton")?.GetComponent<Button>();
            Button continueButton = popupRoot.transform.Find("Panel/ContinueButton")?.GetComponent<Button>();
            TMP_Text message = popupRoot.transform.Find("Panel/MessageText")?.GetComponent<TMP_Text>();
            if (message == null || endButton == null || continueButton == null)
                throw new MissingReferenceException("Ending Popup의 선택 버튼이 없습니다.");

            view.Configure(backdrop, message, endButton, continueButton);
            EditorUtility.SetDirty(view);
            PrefabUtility.SaveAsPrefabAsset(popupRoot, PopupPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(popupRoot);
        }
    }
}
#endif
