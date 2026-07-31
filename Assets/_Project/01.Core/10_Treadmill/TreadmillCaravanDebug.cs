// =============================================================================
// TreadmillCaravanDebug — 프리뷰용 편성 디버그(마차·동물 종류·마릿수 변경)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 · 디버그 도구
//
// [역할] 플레이 중 화면에 편성 패널을 띄워, 중앙 카탈로그(SandboxSharedGameDataCatalog)의
//        마차/동물을 ◀▶로 골라 바꾸고 마릿수를 +/−로 조절한다. 세이브를 읽지 않고
//        TreadmillStage.ShowCustom(...)으로 직접 세운다(프리뷰 확인용).
//
// [주의] IMGUI(OnGUI) 기반 디버그 전용. 아무 오브젝트에나 붙이면 된다(비면 자동 검색).
// =============================================================================

using UnityEngine;

/// <summary>마차·동물 종류·마릿수를 바꿔 세우는 편성 디버그 패널(IMGUI).</summary>
public class TreadmillCaravanDebug : MonoBehaviour
{
    [SerializeField] private TreadmillStage stage;   // 비면 자동 검색
    [SerializeField] private float uiScale = 1.4f;
    [SerializeField] private int count = 2;          // 마릿수
    [SerializeField] private int wagonIdx = 0;       // 선택한 마차
    [SerializeField] private int animalIdx = 0;      // 선택한 동물
    [SerializeField] private int maxCount = 8;

    private ND.Framework.SandboxSharedGameDataCatalog catalog;

    private void Awake()
    {
        if (stage == null) stage = Object.FindFirstObjectByType<TreadmillStage>(FindObjectsInactive.Include);
        catalog = Resources.Load<ND.Framework.SandboxSharedGameDataCatalog>(
                      ND.Framework.SandboxSharedGameDataCatalog.ResourceName);
    }

    private void Start() => Apply();

    private static int Wrap(int i, int n) => n <= 0 ? 0 : (((i % n) + n) % n);

    private void Apply()
    {
        if (stage == null || catalog == null) return;
        var wagons = catalog.Wagons; var animals = catalog.DraftAnimals;
        if (wagons == null || wagons.Length == 0 || animals == null || animals.Length == 0) return;
        wagonIdx = Wrap(wagonIdx, wagons.Length);
        animalIdx = Wrap(animalIdx, animals.Length);
        count = Mathf.Clamp(count, 0, maxCount);
        stage.ShowCustom(wagons[wagonIdx].WagonId, animals[animalIdx].AnimalType, count);
    }

    private static string WagonName(WagonData w) =>
        w == null ? "-" : (!string.IsNullOrEmpty(w.DisplayName) ? w.DisplayName : w.WagonId);
    private static string AnimalName(DraftAnimalData a) =>
        a == null ? "-" : a.AnimalType.ToString();

    private void OnGUI()
    {
        if (stage == null || catalog == null) return;
        var wagons = catalog.Wagons; var animals = catalog.DraftAnimals;
        if (wagons == null || wagons.Length == 0 || animals == null || animals.Length == 0) return;
        wagonIdx = Wrap(wagonIdx, wagons.Length); animalIdx = Wrap(animalIdx, animals.Length);

        float boxW = 220f, boxH = 150f;
        Vector2 anchor = new Vector2(Screen.width - boxW * uiScale - 12f, 12f);   // 우상단
        var oldM = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(uiScale, uiScale), anchor);

        var btn = Style(ref _btn, GUI.skin.button, 12, true);
        var lbl = Style(ref _lbl, GUI.skin.label, 12, false);
        var title = Style(ref _title, GUI.skin.label, 13, true);

        GUILayout.BeginArea(new Rect(anchor.x, anchor.y, boxW, boxH), GUI.skin.box);
        GUILayout.Label("편성 디버그", title);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", btn, GUILayout.Width(30))) { wagonIdx = Wrap(wagonIdx - 1, wagons.Length); Apply(); }
        GUILayout.Label("마차: " + WagonName(wagons[wagonIdx]), lbl, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("▶", btn, GUILayout.Width(30))) { wagonIdx = Wrap(wagonIdx + 1, wagons.Length); Apply(); }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", btn, GUILayout.Width(30))) { animalIdx = Wrap(animalIdx - 1, animals.Length); Apply(); }
        GUILayout.Label("동물: " + AnimalName(animals[animalIdx]), lbl, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("▶", btn, GUILayout.Width(30))) { animalIdx = Wrap(animalIdx + 1, animals.Length); Apply(); }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("−", btn, GUILayout.Width(30))) { count = Mathf.Max(0, count - 1); Apply(); }
        GUILayout.Label("마릿수: " + count, lbl, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("+", btn, GUILayout.Width(30))) { count = Mathf.Min(maxCount, count + 1); Apply(); }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
        GUI.matrix = oldM;
    }

    private static GUIStyle _btn, _lbl, _title;
    private static GUIStyle Style(ref GUIStyle s, GUIStyle basis, int size, bool bold)
    {
        if (s == null) s = new GUIStyle(basis) { fontSize = size, richText = true, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
        return s;
    }
}
