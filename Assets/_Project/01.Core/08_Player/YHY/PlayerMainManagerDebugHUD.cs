// =============================================================================
// PlayerMainManagerDebugHUD — PlayerMainManager 동작 확인용 임시 디버그 화면
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] Play 모드에서 화면 왼쪽 위에 소지금·거점 인벤토리를 실시간으로 보여주고,
//        버튼으로 재화/아이템을 조작해 PlayerMainManager가 제대로 도는지 눈으로 확인한다.
//
// [주의] 정식 UI가 아니라 검증용 임시 스크립트. 실제 재화 라인 HUD가 붙으면 지운다.
//        같은 오브젝트에 PlayerMainManager가 없으면 자동으로 하나 만들어 붙인다.
// =============================================================================

using UnityEngine;

/// <summary>PlayerMainManager를 화면에 띄워 조작·확인하는 임시 디버그 HUD.</summary>
public class PlayerMainManagerDebugHUD : MonoBehaviour
{
    /// <summary>매니저를 견고하게 찾는다 — Instance → 같은 오브젝트 → 씬 전체, 없으면 부착.</summary>
    private PlayerMainManager Resolve()
    {
        // static Instance가 도메인 리로드로 날아가도 컴포넌트 자체는 살아있으므로 재탐색한다.
        PlayerMainManager m = PlayerMainManager.Instance;
        if (m == null) m = GetComponent<PlayerMainManager>();
        if (m == null) m = FindObjectOfType<PlayerMainManager>();
        if (m == null) m = gameObject.AddComponent<PlayerMainManager>();
        return m;
    }

    private void OnGUI()
    {
        PlayerMainManager m = Resolve();
        if (m == null)
        {
            GUI.Label(new Rect(20, 20, 400, 30), "PlayerMainManager 없음");
            return;
        }

        // 큰 글씨 스타일
        GUIStyle box = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
        GUIStyle btn = new GUIStyle(GUI.skin.button) { fontSize = 18 };

        float x = 20, y = 20, w = 360;

        // ── 소지금 ──
        GUI.Box(new Rect(x, y, w, 40), $"소지금(Gold): {m.Gold}", box);
        y += 48;
        if (GUI.Button(new Rect(x, y, 170, 44), "+100 Gold", btn)) m.AddGold(100);
        if (GUI.Button(new Rect(x + 190, y, 170, 44), "-50 Gold", btn)) m.SpendGold(50);
        y += 60;

        // ── 거점 인벤토리 ──
        GUI.Box(new Rect(x, y, w, 40), $"거점 인벤토리 ({m.HomeInventory.Count}종)", box);
        y += 48;
        // 실제로는 TradeItemData(SO)→TradeItemSaveData 변환값을 넘기지만,
        // 여기선 테스트용으로 최소 DTO를 즉석에서 만들어 넣는다.
        if (GUI.Button(new Rect(x, y, 170, 44), "+ 사과(apple)", btn)) m.AddItem(MakeItem("apple", "사과"), 1);
        if (GUI.Button(new Rect(x + 190, y, 170, 44), "- 사과(apple)", btn)) m.RemoveItem("apple", 1);
        y += 52;
        if (GUI.Button(new Rect(x, y, 170, 44), "+ 물약(potion)", btn)) m.AddItem(MakeItem("potion", "물약"), 5);
        if (GUI.Button(new Rect(x + 190, y, 170, 44), "- 물약(potion)", btn)) m.RemoveItem("potion", 5);
        y += 60;

        // 인벤토리 목록 출력 (마차 화물과 동일한 CargoEntrySaveData)
        foreach (ND.Framework.CargoEntrySaveData e in m.HomeInventory)
        {
            string nm = e != null && e.item != null ? e.item.itemName : "?";
            int q = e != null ? e.quantity : 0;
            GUI.Label(new Rect(x, y, w, 26), $"  · {nm} : {q}", box);
            y += 28;
        }
        y += 12;

        // ── 마차 인벤토리 (SaveData.caravan.cargo 참조) ──
        GUI.Box(new Rect(x, y, w, 40), $"마차 화물 ({m.CaravanCargo.Count}종)", box);
        y += 48;
        if (GUI.Button(new Rect(x, y, 170, 44), "+ 마차 사과", btn)) m.AddCargo(MakeItem("apple", "사과"), 1);
        if (GUI.Button(new Rect(x + 190, y, 170, 44), "- 마차 사과", btn)) m.RemoveCargo("apple", 1);
        y += 52;
        // 통일 타입(CargoEntrySaveData) 덕분에 창고→마차 이동이 타입 변환 없이 됨
        if (GUI.Button(new Rect(x, y, 360, 44), "창고 사과 → 마차 이동 (1개)", btn))
        {
            if (m.RemoveItem("apple", 1))                         // 창고에서 빼서
                m.AddCargo(MakeItem("apple", "사과"), 1);          // 마차에 싣기
        }
        y += 60;

        foreach (ND.Framework.CargoEntrySaveData e in m.CaravanCargo)
        {
            string nm = e != null && e.item != null ? e.item.itemName : "?";
            int q = e != null ? e.quantity : 0;
            GUI.Label(new Rect(x, y, w, 26), $"  · {nm} : {q}", box);
            y += 28;
        }

        // ── 마을(건물) — 메인 창구(m.Village)로 접근 = B 구조 증명 ──
        // 메인 매니저가 마을 매니저를 참조로 노출 → 여기선 PlayerMainManager 한 곳만 안다.
        float vx = 400, vy = 20;
        VillageBuildingRegistry village = m.Village;
        GUI.Box(new Rect(vx, vy, 290, 40),
            village != null ? $"마을 건물 ({village.Count}채)  [Village]" : "마을 매니저 없음(씬 미로드)", box);
        vy += 48;
        if (village != null)
        {
            if (village.CatalogCount > 0 &&
                GUI.Button(new Rect(vx, vy, 290, 44), $"{village.GetCatalogName(0)} 짓기/레벨업", btn))
                village.AddOrUpgrade(0);   // 메인 창구 통해 마을 매니저가 실제 관리
            vy += 52;
            for (int i = 0; i < village.Count; i++)
            {
                GUI.Label(new Rect(vx, vy, 290, 26), $"  · {village.GetName(i)} Lv.{village.GetLevel(i)}", box);
                vy += 28;
            }
        }
    }

    /// <summary>테스트용 최소 아이템 DTO 생성(실제 값은 SO에서 변환해 온다).</summary>
    private ND.Framework.TradeItemSaveData MakeItem(string id, string name)
    {
        return new ND.Framework.TradeItemSaveData
        {
            itemId = id,
            itemName = name,
            weight = 1f,
            basePrice = 10,
            maxCount = 99
        };
    }
}
