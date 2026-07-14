// =============================================================================
// QuantityRow — 수량 조절 목록의 한 줄 (동물·아이템 공용)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] "이름/라벨 + [-] 수량 [+]" 한 줄. 동물 선택·아이템 적재 등 수량을 고르는
//        모든 목록에서 공용으로 쓴다. 수량이 바뀌면 OnChanged(항목ID, 새 수량)로 상위에 알린다.
//        규칙 검증(슬롯·무게·동일종류)은 상위 패널/CaravanValidator 담당 — 여긴 순수 UI.
//
// [쓰는 법 — 유니티]
//   행 프리팹 루트에 붙이고 nameText/countText/minusButton/plusButton 연결.
//   상위 패널이 Setup(...)으로 초기화한다(코드 호출).
// =============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>수량 조절 목록의 한 줄 — 라벨 표시 + [-]/[+] 수량. 동물·아이템 공용. [1차 빌드]</summary>
public class QuantityRow : MonoBehaviour
{
    [Header("표시")]
    [SerializeField] private TMP_Text nameText;    // 라벨 (예: "이름 (종류)" 또는 "아이템명 (단위무게)")
    [SerializeField] private TMP_Text countText;   // 현재 수량

    [Header("버튼")]
    [SerializeField] private Button minusButton;   // 수량 -1
    [SerializeField] private Button plusButton;    // 수량 +1

    private string itemId;   // 이 행이 나타내는 항목 ID
    private int count;       // 현재 수량 (0 = 미선택)
    private int maxCount;    // 이 항목의 상한 (보유 수 등)

    /// <summary>수량이 바뀔 때. 인자 = (항목 ID, 새 수량).</summary>
    public Action<string, int> OnChanged;

    /// <summary>상위 패널이 이 행을 초기화한다. (id, 라벨, 시작 수량, 상한)</summary>
    public void Setup(string itemId, string label, int startCount, int maxCount)
    {
        this.itemId = itemId;
        this.maxCount = maxCount;
        this.count = Mathf.Clamp(startCount, 0, maxCount);

        if (nameText != null) nameText.text = label;

        // 버튼 리스너는 초기화 때마다 갈아끼운다(행 프리팹 재사용 대비).
        if (minusButton != null)
        {
            minusButton.onClick.RemoveAllListeners();
            minusButton.onClick.AddListener(() => Change(-1));
        }
        if (plusButton != null)
        {
            plusButton.onClick.RemoveAllListeners();
            plusButton.onClick.AddListener(() => Change(+1));
        }

        RefreshView();
    }

    /// <summary>외부에서 수량을 강제로 맞춘다(상위가 슬롯/무게로 제한할 때). 알림은 보내지 않음.</summary>
    public void SetCountSilently(int value)
    {
        count = Mathf.Clamp(value, 0, maxCount);
        RefreshView();
    }

    /// <summary>수량을 delta만큼 바꾼다(0~상한 클램프). 값이 실제로 바뀌면 알림.</summary>
    private void Change(int delta)
    {
        int next = Mathf.Clamp(count + delta, 0, maxCount);
        if (next == count) return;   // 상·하한에 걸려 변화 없으면 무시
        count = next;
        RefreshView();
        OnChanged?.Invoke(itemId, count);
    }

    /// <summary>수량 텍스트·버튼 활성 상태를 현재 값에 맞춘다.</summary>
    private void RefreshView()
    {
        if (countText != null) countText.text = count.ToString();
        if (minusButton != null) minusButton.interactable = count > 0;
        if (plusButton != null) plusButton.interactable = count < maxCount;
    }
}
