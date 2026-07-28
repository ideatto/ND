// =============================================================================
// BuildingPlacementController — 마을 건물 배치(드래그 이동 + 회전)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 마을이 RenderTexture(RawImage)로 보여지므로, 유저가 화면에서 건물을
//        클릭/드래그해도 그건 UI(RawImage) 클릭이다. 이 컨트롤러가 그 클릭을
//        "마을 카메라 광선"으로 변환해서 3D 건물을 집고, 바닥으로 드래그해 옮기고,
//        스크롤로 회전시킨다.
//
// [부착] 마을을 표시하는 RawImage(VillageView) 오브젝트에 붙인다.
//        RawImage.raycastTarget = true 여야 포인터 이벤트를 받는다.
//
// [의존] 건물 프리팹 = PlaceableBuilding + BoxCollider(집기용), 바닥 = Collider(놓기용).
//        마을 카메라는 RawImage의 RenderTexture를 그리는 카메라를 런타임에 찾는다.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>RawImage 클릭 → 마을 카메라 광선 → 건물 집기/드래그 이동/스크롤 회전.</summary>
public class BuildingPlacementController : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IScrollHandler
{
    [SerializeField] private RawImage view;            // RT를 그리는 RawImage(비면 자기 자신)
    [SerializeField] private Camera villageCamera;     // 마을 카메라(비면 런타임 탐색)
    [SerializeField] private Color highlightTint = new Color(1f, 0.85f, 0.4f); // 선택 하이라이트 색

    [Header("그리드 배치")]
    [SerializeField, Min(1)] private int gridWidth = 24;   // 마을 가로 칸 수(24×1.25m=30m, 바닥 크기)
    [SerializeField, Min(1)] private int gridHeight = 24;  // 마을 세로 칸 수
    // [그리드] 회전은 90° 단위(칸 방향 4개)로만 — 자유 각도면 칸에 안 맞음.
    private const float GridRotateStep = 90f;

    [Header("카메라 줌")]
    [SerializeField] private float zoomStep = 1f;     // 스크롤 1노치당 줌 변화(ortho size)
    [SerializeField] private float zoomMin = 4f;      // 최대 확대(가까이) — orthographicSize 하한
    [SerializeField] private float zoomMax = 15f;     // 최대 축소(멀리) — 바닥 전체 보임
    private Camera villageCam;                         // 줌 대상(마을 카메라, 런타임 탐색)

    private Transform selected;   // 현재 집은 건물 루트
    private Camera uiCamera;      // 캔버스 렌더 카메라(Overlay면 null)
    private VillageGrid grid;     // 셀 점유 격자(겹침 판정을 물리 대신 이걸로)
    private VillageGridOverlay gridOverlay;   // 편집 모드 격자선(선택 시 표시)

    // 선택 하이라이트 복원용(집은 건물의 렌더러 원래 색 저장)
    private readonly List<Renderer> tintedRenderers = new List<Renderer>();
    private readonly List<Color> tintedOriginals = new List<Color>();

    private void Awake()
    {
        if (view == null) view = GetComponent<RawImage>();
        Canvas canvas = GetComponentInParent<Canvas>();
        uiCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;

        grid = new VillageGrid(gridWidth, gridHeight);
        BuildGridOverlay();   // 편집 모드용 격자선 오버레이 생성(기본 숨김)

        // [중요] 마을(Village_Home)은 additive로 나중에 로드되므로, Awake 시점엔 건물이 없다.
        //        씬이 로드될 때마다 등록을 다시 시도하고, 이미 로드돼 있으면 지금 바로 등록한다.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnAnySceneLoaded;
        RegisterExistingBuildings();   // 이미 로드된 상태면 지금 등록
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnAnySceneLoaded;
    }

    private float newBuildingScanTimer;

    // 새로 지은 건물이 클릭 없이도 바로 격자에 앉도록, 주기적으로 미등록 건물을 흡수한다.
    // (건물 추가는 Registry가 하고 등록은 여기라 분리돼 있어, 폴링으로 연결)
    private void Update()
    {
        newBuildingScanTimer -= Time.deltaTime;
        if (newBuildingScanTimer <= 0f)
        {
            newBuildingScanTimer = 0.25f;   // 0.25초마다 새 건물 체크(건물 수 적어 부담 없음)
            RegisterExistingBuildings();    // registered 셋으로 신규만 처리
        }
    }

    private bool npcSpawned;

    private void OnAnySceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        RegisterExistingBuildings();   // 마을 씬이 올라온 뒤 건물들을 등록·정렬

        // 마을 씬이 올라왔고 건물 등록이 끝났으면, NPC를 한 번만 스폰(건물 칸을 피해 배회).
        if (!npcSpawned && registered.Count > 0)
        {
            villageScene = s;
            villageSceneValid = s.IsValid();
            SpawnNpcs();
            npcSpawned = true;
        }
    }

    // 이미 격자에 등록한 건물(중복 등록 방지)
    private readonly HashSet<Transform> registered = new HashSet<Transform>();

    // NPC들이 공유하는 건물 목록(새 건물이 지어지면 여기 추가 → 모든 NPC가 즉시 인식)
    private readonly List<PlaceableBuilding> npcBuildingList = new List<PlaceableBuilding>();

    /// <summary>
    /// 씬에 배치된 건물 중 아직 등록 안 된 것을 격자에 등록하며 칸에 정렬한다.
    /// 서로 겹쳐서 시작한 건물은 가장 가까운 빈 칸으로 밀어내 겹침을 해소한다.
    /// (마을 씬이 늦게 로드되므로 여러 번 호출되어도 안전하게 — 신규만 처리.)
    /// </summary>
    private void RegisterExistingBuildings()
    {
        foreach (PlaceableBuilding pb in FindObjectsByType<PlaceableBuilding>(FindObjectsSortMode.None))
        {
            Transform t = pb.transform;
            if (!registered.Add(t)) continue;   // 이미 등록된 건물은 건너뜀

            // 신규 건물: NPC 공유 목록에 추가 + 욕구 태그 부여(모든 NPC가 즉시 인식)
            npcBuildingList.Add(pb);
            AssignNeed(pb);

            pb.GetRotatedCells(out int sx, out int sz);

            // 현재 위치 → 칸 환산(건물 중심이 그 영역 한가운데 오도록 왼쪽아래 칸 역산). 오프셋은 월드 단위라 CellSize 곱한다.
            Vector3 corner = t.position - new Vector3(sx * 0.5f, 0f, sz * 0.5f) * VillageGrid.CellSize;
            grid.WorldToCell(corner, out int cx, out int cz);

            // 그 자리가 막혀 있으면(다른 건물과 겹침) 가장 가까운 빈 칸을 찾는다.
            if (!grid.CanPlace(cx, cz, sx, sz, t))
                grid.FindNearestFree(cx, cz, sx, sz, t, out cx, out cz);

            grid.Occupy(cx, cz, sx, sz, t);
            t.position = grid.CellToWorldCenter(cx, cz, sx, sz);   // 칸 중심으로 정렬(겹침 해소)
        }
    }

    // ── 포인터 이벤트 ──
    /// <summary>누르는 순간: 광선으로 건물을 집는다(없으면 선택 해제).</summary>
    public void OnPointerDown(PointerEventData e)
    {
        // 건물 추가로 새로 생긴 건물을 격자에 등록·정렬(런타임 생성분은 sceneLoaded를 안 타므로 여기서 흡수).
        RegisterExistingBuildings();

        if (IsOverButton(e.position)) return;   // 회전 버튼 클릭이면 선택 해제 안 함
        if (!TryMakeRay(e.position, out Ray ray)) return;

        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (RaycastHit h in hits)
        {
            PlaceableBuilding pb = h.collider.GetComponentInParent<PlaceableBuilding>();
            if (pb != null && h.distance < bestDist) { bestDist = h.distance; best = pb.transform; }
        }
        SetSelected(best);   // 빈 곳 클릭이면 null → 선택 해제
    }

    /// <summary>드래그: 건물 선택 중이면 건물 이동, 아니면 마을(카메라) 이동.</summary>
    public void OnDrag(PointerEventData e)
    {
        if (selected == null) { PanCamera(e.delta); return; }   // 빈 곳 드래그 → 마을 패닝
        if (IsOverButton(e.position)) return;   // 버튼 위에서의 드래그로 건물이 튀지 않게
        if (!TryMakeRay(e.position, out Ray ray)) return;

        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        // 가까운 것부터 정렬해 바닥(건물 아닌 것) 첫 히트를 찾는다.
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.transform.IsChildOf(selected)) continue;                         // 자기 자신
            if (h.collider.GetComponentInParent<PlaceableBuilding>() != null) continue; // 다른 건물
            SnapToGrid(selected, h.point);   // 광선 지점 → 가장 가까운 칸에 스냅(빈 칸일 때만)
            return;
        }
    }

    /// <summary>
    /// 광선이 바닥에 맞은 지점을, 건물이 들어갈 "가장 가까운 칸"으로 스냅해 이동한다.
    /// 자기 자신은 무시하고 그 칸들이 비었을 때만 옮긴다(겹침 판정 = 격자 조회).
    /// </summary>
    private void SnapToGrid(Transform building, Vector3 hitPoint)
    {
        PlaceableBuilding pb = building.GetComponent<PlaceableBuilding>();
        int sx = 1, sz = 1;
        if (pb != null) pb.GetRotatedCells(out sx, out sz);

        // 히트 지점이 건물 영역의 "중심"이 되도록, 왼쪽아래 칸을 역산한다.
        Vector3 corner = hitPoint - new Vector3(sx * 0.5f, 0f, sz * 0.5f) * VillageGrid.CellSize;
        grid.WorldToCell(corner, out int cx, out int cz);

        if (!grid.CanPlace(cx, cz, sx, sz, building)) return;   // 칸이 막혔으면 이동 안 함

        grid.Clear(building);                                    // 이전 칸 비우고
        grid.Occupy(cx, cz, sx, sz, building);                   // 새 칸 점유
        building.position = grid.CellToWorldCenter(cx, cz, sx, sz);  // 칸 중심으로 스냅
    }

    /// <summary>스크롤: 선택한 건물을 90°씩 회전(격자 방향).</summary>
    public void OnScroll(PointerEventData e)
    {
        float dir = Mathf.Sign(e.scrollDelta.y);
        if (dir == 0f) return;

        if (selected != null)
            ApplyYaw(GridRotateStep * dir);   // 건물 선택 중 → 회전
        else
            ApplyZoom(dir);                    // 빈 곳 → 카메라 줌
    }

    /// <summary>마을 카메라 줌(직교 size 조절). dir>0=확대(size↓), dir&lt;0=축소(size↑).</summary>
    private void ApplyZoom(float dir)
    {
        Camera cam = ResolveVillageCamera();
        if (cam == null || !cam.orthographic) return;

        // 위로 스크롤(dir>0)이면 확대이므로 size를 줄인다.
        float next = cam.orthographicSize - dir * zoomStep;
        cam.orthographicSize = Mathf.Clamp(next, zoomMin, zoomMax);
    }

    /// <summary>마을(RenderTexture) 카메라를 찾는다(줌용, 한 번 찾으면 캐시).</summary>
    private Camera ResolveVillageCamera()
    {
        if (villageCam != null) return villageCam;
        villageCam = ResolveCamera();   // 기존 광선용 카메라 탐색 재사용(같은 마을 카메라)
        return villageCam;
    }

    /// <summary>
    /// 카메라를 "현재 위치"를 새 팬 중심(camHome)으로 재설정한다.
    /// 다른 마을로 카메라를 이동(TradeTownCameraMover)한 뒤 호출 →
    /// 팬 범위(panRange)가 옛 거점이 아니라 지금 보는 마을 기준이 되어, 드래그해도 안 끌려온다.
    /// </summary>
    public void RecenterPanHome()
    {
        Camera cam = ResolveVillageCamera();
        if (cam == null) return;
        camHome = cam.transform.position;
        camHomeSet = true;
    }

    [Header("카메라 이동(패닝)")]
    [SerializeField] private float panSpeed = 0.01f;   // 드래그 픽셀당 이동량(월드 m)
    [SerializeField] private float panRange = 12f;     // 마을 중심에서 벗어날 수 있는 최대 거리(m)
    private Vector3 camHome;                             // 카메라 기본(중심) 위치 — 범위 제한 기준
    private bool camHomeSet;

    /// <summary>
    /// 빈 곳 드래그로 마을 카메라를 평행 이동한다. 화면 드래그 방향과 반대로 카메라를 밀어
    /// "바닥을 손으로 잡고 끄는" 느낌을 준다. 카메라 로컬축 기준이라 비스듬한 뷰에서도 맞다.
    /// 마을을 너무 벗어나지 않도록 기본 위치에서 panRange 안으로 제한한다.
    /// </summary>
    private void PanCamera(Vector2 dragDelta)
    {
        Camera cam = ResolveVillageCamera();
        if (cam == null) return;

        if (!camHomeSet) { camHome = cam.transform.position; camHomeSet = true; }   // 최초 위치 기억

        // 줌(ortho size)에 비례해 이동량 보정 — 확대 상태에선 조금, 축소 상태에선 많이 움직이게.
        float scale = panSpeed * (cam.orthographicSize / 6f);

        // 카메라 로컬 right/up을 바닥(XZ)에 투영해 이동축을 만든다(비스듬한 뷰 대응).
        Vector3 right = cam.transform.right;
        Vector3 up = cam.transform.up;
        right.y = 0f; up.y = 0f;
        right.Normalize(); up.Normalize();

        // 드래그 반대 방향으로 카메라 이동(바닥을 끄는 느낌).
        Vector3 move = (-right * dragDelta.x - up * dragDelta.y) * scale;
        Vector3 next = cam.transform.position + move;

        // 기본 위치에서 panRange를 넘어가면 원 안으로 당겨 마을을 벗어나지 않게 한다(높이 y는 고정).
        Vector3 offset = next - camHome;
        offset.y = 0f;
        if (offset.magnitude > panRange) offset = offset.normalized * panRange;
        cam.transform.position = new Vector3(camHome.x + offset.x, camHome.y, camHome.z + offset.z);
    }

    private const float BtnW = 100f, BtnH = 100f, BtnOff = 150f;   // 회전 버튼 크기 + 중심에서 좌우 간격

    // 선택된 건물의 양 옆에 작은 좌/우 회전 버튼 표시(건물 따라다님. 임시 IMGUI, 추후 uGUI 교체 가능).
    private void OnGUI()
    {
        if (!TryButtonRects(out Rect left, out Rect right)) return;
        GUIStyle s = new GUIStyle(GUI.skin.button) { fontSize = 52 };
        // 화면좌표(좌하단 원점) → GUI좌표(좌상단 원점): y 뒤집기
        Rect lg = new Rect(left.x, Screen.height - left.y - left.height, left.width, left.height);
        Rect rg = new Rect(right.x, Screen.height - right.y - right.height, right.width, right.height);
        if (GUI.Button(lg, "◀", s)) RotateLeft();
        if (GUI.Button(rg, "▶", s)) RotateRight();
    }

    /// <summary>선택 건물 기준 좌/우 버튼의 화면좌표(좌하단 원점) 사각형. 선택 없거나 화면 밖이면 false.</summary>
    private bool TryButtonRects(out Rect left, out Rect right)
    {
        left = default(Rect); right = default(Rect);
        if (selected == null) return false;
        Collider col = selected.GetComponentInChildren<Collider>();
        Vector3 center = col != null ? col.bounds.center : selected.position;
        if (!TryWorldToScreen(center, out Vector2 sp)) return false;
        left = new Rect(sp.x - BtnOff - BtnW * 0.5f, sp.y - BtnH * 0.5f, BtnW, BtnH);
        right = new Rect(sp.x + BtnOff - BtnW * 0.5f, sp.y - BtnH * 0.5f, BtnW, BtnH);
        return true;
    }

    /// <summary>포인터가 회전 버튼 위에 있나(있으면 집기·드래그를 무시해 버튼 클릭만 처리).</summary>
    private bool IsOverButton(Vector2 screenPos)
    {
        if (!TryButtonRects(out Rect left, out Rect right)) return false;
        return left.Contains(screenPos) || right.Contains(screenPos);
    }

    /// <summary>마을 안 월드좌표 → 화면 좌표(RawImage/RT 경유). 카메라 뒤면 false.</summary>
    private bool TryWorldToScreen(Vector3 world, out Vector2 screen)
    {
        screen = default(Vector2);
        Camera cam = ResolveCamera();
        if (cam == null || view == null) return false;

        Vector3 vp = cam.WorldToViewportPoint(world);
        if (vp.z <= 0f) return false;

        RectTransform rt = view.rectTransform;
        Rect r = rt.rect;
        Vector2 local = new Vector2(Mathf.Lerp(r.xMin, r.xMax, vp.x), Mathf.Lerp(r.yMin, r.yMax, vp.y));
        screen = RectTransformUtility.WorldToScreenPoint(uiCamera, rt.TransformPoint(local));
        return true;
    }

    /// <summary>◀ 버튼(왼쪽으로 90°).</summary>
    public void RotateLeft() { ApplyYaw(GridRotateStep); }

    /// <summary>▶ 버튼(오른쪽으로 90°).</summary>
    public void RotateRight() { ApplyYaw(-GridRotateStep); }

    /// <summary>
    /// 선택 건물을 Y축 90° 단위로 회전. 회전하면 가로·세로 칸이 뒤바뀌므로,
    /// 새 방향이 현재 자리에 들어갈 수 있을 때만 회전을 확정하고 격자 점유를 갱신한다.
    /// 안 들어가면(옆 건물과 겹침) 회전을 취소한다.
    /// </summary>
    private void ApplyYaw(float deg)
    {
        if (selected == null) return;

        PlaceableBuilding pb = selected.GetComponent<PlaceableBuilding>();
        Quaternion before = selected.rotation;
        float y = Mathf.Round((selected.eulerAngles.y + deg) / 90f) * 90f;   // 90° 격자로 스냅
        selected.rotation = Quaternion.Euler(0f, y, 0f);

        if (pb == null) return;   // 칸 정보 없으면 회전만 (그리드 무관 오브젝트)

        // 회전 후 점유 칸을 다시 계산해 배치 가능하면 확정, 아니면 되돌린다.
        pb.GetRotatedCells(out int sx, out int sz);
        Vector3 corner = selected.position - new Vector3(sx * 0.5f, 0f, sz * 0.5f) * VillageGrid.CellSize;
        grid.WorldToCell(corner, out int cx, out int cz);

        if (grid.CanPlace(cx, cz, sx, sz, selected))
        {
            grid.Clear(selected);
            grid.Occupy(cx, cz, sx, sz, selected);
            selected.position = grid.CellToWorldCenter(cx, cz, sx, sz);   // 회전 후에도 칸 정렬 유지
        }
        else
        {
            selected.rotation = before;   // 회전하면 겹침 → 취소
        }
    }

    // ── 선택 + 하이라이트 ──
    /// <summary>선택을 바꾸며, 이전 건물은 원래 색으로, 새 건물은 하이라이트 색으로.
    /// 선택되면 편집 모드 → 바닥 격자 표시, 해제되면 숨김.</summary>
    private void SetSelected(Transform t)
    {
        if (selected == t) return;
        ClearHighlight();
        selected = t;
        if (selected != null) ApplyHighlight(selected);

        // 편집 모드 격자 on/off
        if (gridOverlay != null)
        {
            if (selected != null) gridOverlay.Show();
            else gridOverlay.Hide();
        }
    }

    /// <summary>격자선 오버레이 오브젝트를 만들어 격자 크기에 맞춰 구성한다(기본 숨김).</summary>
    private void BuildGridOverlay()
    {
        var go = new GameObject("VillageGridOverlay");
        gridOverlay = go.AddComponent<VillageGridOverlay>();
        gridOverlay.Build(gridWidth, gridHeight, VillageGrid.CellSize);
    }

    [Header("NPC")]
    [SerializeField] private int npcCount = 3;               // 스폰할 NPC 수
    [Tooltip("스폰에 사용할 NPC 프리팹들(VillageNpc 컴포넌트 보유). 여기서 넣고 빼면 등장 NPC가 바뀐다. 비면 큐브로 폴백.")]
    [SerializeField] private List<GameObject> npcPrefabs = new List<GameObject>();
    [SerializeField] private float npcCubeSize = 0.6f;       // 폴백 큐브 크기(프리팹 없을 때)

    /// <summary>NPC를 npcCount만큼 생성해 grid + 공유 건물 목록을 주입한다.
    /// npcPrefabs에서 골라 스폰하고, 비어 있으면 큐브로 폴백한다.</summary>
    private void SpawnNpcs()
    {
        // 스폰 시점의 건물은 RegisterExistingBuildings가 이미 npcBuildingList에 넣고 태그했다.
        for (int i = 0; i < npcCount; i++)
        {
            GameObject go = MakeNpcObject(i);

            // 마을 씬으로 옮기고(마을 카메라가 비추게), 시작 위치는 대충 중앙 근처
            if (villageSceneValid) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, villageScene);
            go.transform.position = new Vector3(i * 0.5f, 0f, 0f);

            var npc = go.GetComponent<VillageNpc>();
            if (npc == null) npc = go.AddComponent<VillageNpc>();
            npc.Init(grid, npcBuildingList);
        }
    }

    /// <summary>NPC 오브젝트 하나 생성 — 프리팹 목록에서 순환 선택, 없으면 큐브 폴백.</summary>
    private GameObject MakeNpcObject(int index)
    {
        // 유효한 프리팹만 추림
        var valid = npcPrefabs != null ? npcPrefabs.FindAll(p => p != null) : null;
        if (valid != null && valid.Count > 0)
        {
            GameObject prefab = valid[index % valid.Count];   // 순환 선택(여러 종류 섞이게)
            GameObject go = Instantiate(prefab);
            go.name = prefab.name + "_" + index;
            // 프리팹에 콜라이더가 있으면 제거(건물 집기 광선 방해 방지)
            foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
            return go;
        }

        // 폴백: 큐브
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "VillageNpc_" + index;
        cube.transform.localScale = new Vector3(npcCubeSize, npcCubeSize, npcCubeSize);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.9f, 0.3f + 0.2f * index, 0.2f);
        cube.GetComponent<Renderer>().sharedMaterial = mat;
        Destroy(cube.GetComponent<Collider>());
        return cube;
    }

    private UnityEngine.SceneManagement.Scene villageScene;
    private bool villageSceneValid;

    /// <summary>
    /// 건물 하나에 "채워주는 욕구"를 태그로 붙인다(임시 매핑, 이름 기준).
    /// 빵집→허기 / 집 계열→체력 / 나머지→심심. 이미 태그가 있으면 건드리지 않는다.
    /// </summary>
    private void AssignNeed(PlaceableBuilding b)
    {
        if (b == null || b.GetComponent<BuildingNeedProvider>() != null) return;

        var provider = b.gameObject.AddComponent<BuildingNeedProvider>();
        string n = b.gameObject.name;

        if (n.Contains("빵집") || n.Contains("Bakery"))
            provider.satisfies = NeedType.Hunger;                              // 빵집 = 허기
        else if (n.Contains("오두막") || n.Contains("Cottage") || n.Contains("통나무") || n.Contains("Hut"))
            provider.satisfies = NeedType.Energy;                              // 집 계열 = 체력(휴식)
        else
            provider.satisfies = NeedType.Fun;                                 // 상점·창고·목장·풍차 등 = 심심
    }

    private void ApplyHighlight(Transform root)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            tintedRenderers.Add(r);
            tintedOriginals.Add(r.material.color);
            r.material.color = highlightTint;   // 런타임이라 material 인스턴스화됨(원본 에셋 영향 없음)
        }
    }

    private void ClearHighlight()
    {
        for (int i = 0; i < tintedRenderers.Count; i++)
            if (tintedRenderers[i] != null) tintedRenderers[i].material.color = tintedOriginals[i];
        tintedRenderers.Clear();
        tintedOriginals.Clear();
    }

    // ── RawImage 스크린좌표 → 마을 카메라 광선 ──
    private bool TryMakeRay(Vector2 screenPos, out Ray ray)
    {
        ray = default(Ray);
        Camera cam = ResolveCamera();
        if (cam == null || view == null) return false;

        RectTransform rt = view.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, uiCamera, out Vector2 local))
            return false;

        // RawImage 로컬좌표 → 0..1 뷰포트 UV
        Rect r = rt.rect;
        float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

        ray = cam.ViewportPointToRay(new Vector3(u, v, 0f));
        return true;
    }

    /// <summary>RawImage의 RenderTexture를 그리는 카메라를 찾는다.</summary>
    private Camera ResolveCamera()
    {
        if (villageCamera != null) return villageCamera;
        RenderTexture rtTex = view != null ? view.texture as RenderTexture : null;
        foreach (Camera c in Camera.allCameras)
        {
            if (c.targetTexture == null) continue;
            if (rtTex == null || c.targetTexture == rtTex) { villageCamera = c; return c; }
        }
        return null;
    }
}
