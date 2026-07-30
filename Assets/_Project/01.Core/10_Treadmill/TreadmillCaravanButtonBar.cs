// =============================================================================
// TreadmillCaravanButtonBar — 보유 캐러밴 수만큼만 마차 버튼을 보이게
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템
//
// [역할] 우하단 "마차N" 버튼들은 씬에 4개 미리 놓여 있지만, 실제로는 플레이어가 보유한
//        캐러밴(SaveData.caravans) 수만큼만 보여야 한다. 이 컴포넌트가 세이브를 읽어
//        캐러밴이 있는 버튼만 활성화하고, 각 버튼에 실제 caravanId를 매핑한다.
//        (예: 캐러밴 1개 → 마차1만 보임, 2·3·4는 숨김)
//
// [부착] 마차 버튼들을 담은 컨테이너(TreadmillCaravanButtons)에 붙인다.
// [주의] 세이브가 아직 없으면(에디트/초기) 버튼을 건드리지 않는다.
// =============================================================================

using UnityEngine;

/// <summary>보유 캐러밴 수에 맞춰 마차 버튼을 보이고 실제 캐러밴에 매핑한다.</summary>
public class TreadmillCaravanButtonBar : MonoBehaviour
{
    private void OnEnable() => Refresh();
    private void Start() => Refresh();

    /// <summary>세이브의 캐러밴 목록을 읽어 버튼 노출/매핑을 갱신한다.</summary>
    public void Refresh()
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        var caravans = save != null ? save.caravans : null;
        if (caravans == null) return;   // 세이브 없음(에디트/초기) → 그대로 둠

        var btns = GetComponentsInChildren<TreadmillCaravanButton>(true);
        for (int i = 0; i < btns.Length; i++)
        {
            bool exists = i < caravans.Count;
            btns[i].gameObject.SetActive(exists);                 // 없는 캐러밴 버튼은 숨김
            if (exists) btns[i].SetCaravan(caravans[i].caravanId, "마차 " + (i + 1));
        }
    }
}
