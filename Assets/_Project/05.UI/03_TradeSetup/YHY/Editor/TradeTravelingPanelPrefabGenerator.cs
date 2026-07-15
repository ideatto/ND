#if UNITY_EDITOR
using TMPro; using UnityEditor; using UnityEngine; using UnityEngine.UI;
public static class TradeTravelingPanelPrefabGenerator
{
 // Reimporting this generator lets the open Unity Editor create the missing handoff prefab.
 const string Path="Assets/_Project/08.Prefabs/UI/Trade/TradeTravelingPanel.prefab";
 [InitializeOnLoadMethod] static void Once(){if(AssetDatabase.LoadAssetAtPath<GameObject>(Path)==null)EditorApplication.delayCall+=Generate;}
 [MenuItem("ND/UI/Generate Trade Traveling Panel")] public static void Generate(){
  var root=new GameObject("TradeTravelingPanel",typeof(RectTransform),typeof(Image),typeof(VerticalLayoutGroup)); root.GetComponent<Image>().color=new Color(.08f,.08f,.1f,.9f);
  var route=Text(root.transform,"RouteText","출발지 → 목적지",32); var remain=Text(root.transform,"RemainingTimeText","남은 시간 --:--",26);
  var slider=new GameObject("Progress",typeof(RectTransform),typeof(Slider)).GetComponent<Slider>(); slider.transform.SetParent(root.transform,false); slider.minValue=0;slider.maxValue=1;
  var food=Text(root.transform,"FoodText","현재 식량 --",24); var status=Text(root.transform,"StatusText","무역 진행 중",22);
  var c=root.AddComponent<TradeTravelingPanelController>();var s=new SerializedObject(c);s.FindProperty("routeText").objectReferenceValue=route;s.FindProperty("remainingTimeText").objectReferenceValue=remain;s.FindProperty("foodText").objectReferenceValue=food;s.FindProperty("statusText").objectReferenceValue=status;s.FindProperty("progressSlider").objectReferenceValue=slider;s.ApplyModifiedPropertiesWithoutUndo();
  PrefabUtility.SaveAsPrefabAsset(root,Path);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();AssetDatabase.Refresh();}
 static TMP_Text Text(Transform p,string n,string v,float z){var g=new GameObject(n,typeof(RectTransform),typeof(TextMeshProUGUI),typeof(LayoutElement));g.transform.SetParent(p,false);var t=g.GetComponent<TMP_Text>();t.text=v;t.fontSize=z;t.color=Color.white;g.GetComponent<LayoutElement>().preferredHeight=54;return t;}
}
#endif
