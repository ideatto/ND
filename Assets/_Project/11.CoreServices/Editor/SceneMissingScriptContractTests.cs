using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneMissingScriptContractTests
{
    private const string BootScenePath = "Assets/_Project/07.Scenes/01_Boot/Boot.unity";
    private const string TitleScenePath = "Assets/_Project/07.Scenes/02_Title/Title.unity";

    [TestCase(BootScenePath)]
    [TestCase(TitleScenePath)]
    public void Scene_HasNoMissingScripts(string scenePath)
    {
        Assert.That(
            AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath),
            Is.Not.Null,
            $"Scene asset is missing: {scenePath}");

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);
            Assert.That(scene.isDirty, Is.False, "Opening the scene must not mutate it.");

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    GameObject target = transforms[index].gameObject;
                    Assert.That(
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target),
                        Is.Zero,
                        $"Missing script found in '{scenePath}' at '{GetHierarchyPath(target.transform)}'.");
                }
            }

            Assert.That(scene.isDirty, Is.False, "Contract inspection must not dirty the scene.");
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
