using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.Title.Editor
{
    [InitializeOnLoad]
    internal static class InGameSettingsPrefabBuilder
    {
        private const string SourcePath = "Assets/_Project/05.UI/02_Title/Prefabs/TitleSettingsCanvas.prefab";
        private const string TargetPath = "Assets/_Project/05.UI/02_Title/Prefabs/InGameSettingPannel.prefab";

        static InGameSettingsPrefabBuilder()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath) == null)
                EditorApplication.delayCall += Create;
        }

        [MenuItem("Tools/ND/Settings/Create InGame Settings Prefab")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath) == null &&
                !AssetDatabase.CopyAsset(SourcePath, TargetPath))
                throw new InvalidOperationException($"Failed to copy {SourcePath} to {TargetPath}.");

            GameObject root = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                root.name = "InGameSettingPannel";
                RemoveRootComponent<Canvas>(root);
                RemoveRootComponent<CanvasScaler>(root);
                RemoveRootComponent<GraphicRaycaster>(root);
                UnpackNestedPrefabs(root);

                SettingsUIManager manager = root.GetComponentInChildren<SettingsUIManager>(true);
                if (manager == null) throw new InvalidOperationException("SettingsUIManager is missing.");

                SerializedObject serializedManager = new SerializedObject(manager);
                GameObject optionPanel = serializedManager.FindProperty("optionPanel").objectReferenceValue as GameObject;
                if (optionPanel == null) throw new InvalidOperationException("optionPanel is not assigned.");
                optionPanel.SetActive(false);

                WireSlider(serializedManager, "bgmSlider", manager.SetBgmVolume);
                WireSlider(serializedManager, "sfxSlider", manager.SetSfxVolume);
                WireSlider(serializedManager, "uiSfxSlider", manager.SetUiSfxVolume);
                WireToggle(serializedManager, "bgmToggle", manager.SetBgmEnabled);
                WireToggle(serializedManager, "sfxToggle", manager.SetSfxEnabled);
                WireToggle(serializedManager, "uiSfxToggle", manager.SetUiSfxEnabled);
                WireDropdown(serializedManager, "windowModeDropdown", manager.SetWindowMode);
                WireDropdown(serializedManager, "resolutionDropdown", manager.SetResolution);

                PrefabUtility.SaveAsPrefabAsset(root, TargetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[InGameSettingsPrefabBuilder] Created {TargetPath}");
        }

        private static void WireSlider(SerializedObject manager, string propertyName, UnityEngine.Events.UnityAction<float> listener)
        {
            Slider control = RequireControl<Slider>(manager, propertyName);
            while (control.onValueChanged.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(control.onValueChanged, 0);
            UnityEventTools.AddPersistentListener(control.onValueChanged, listener);
        }

        private static void WireToggle(SerializedObject manager, string propertyName, UnityEngine.Events.UnityAction<bool> listener)
        {
            Toggle control = RequireControl<Toggle>(manager, propertyName);
            while (control.onValueChanged.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(control.onValueChanged, 0);
            UnityEventTools.AddPersistentListener(control.onValueChanged, listener);
        }

        private static void WireDropdown(SerializedObject manager, string propertyName, UnityEngine.Events.UnityAction<int> listener)
        {
            TMP_Dropdown control = RequireControl<TMP_Dropdown>(manager, propertyName);
            while (control.onValueChanged.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(control.onValueChanged, 0);
            UnityEventTools.AddPersistentListener(control.onValueChanged, listener);
        }

        private static T RequireControl<T>(SerializedObject manager, string propertyName) where T : Component
        {
            T control = manager.FindProperty(propertyName).objectReferenceValue as T;
            if (control == null) throw new InvalidOperationException($"{propertyName} is not assigned.");
            return control;
        }

        private static void UnpackNestedPrefabs(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in transforms)
            {
                if (transform == root.transform || !PrefabUtility.IsOutermostPrefabInstanceRoot(transform.gameObject))
                    continue;
                PrefabUtility.UnpackPrefabInstance(
                    transform.gameObject,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }
        }

        private static void RemoveRootComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component != null) UnityEngine.Object.DestroyImmediate(component, true);
        }
    }
}
