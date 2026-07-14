// =============================================================================
// WagonSelectPopup — 웨건 선택 팝업 (동물 화면의 Wagon Info [Edit]에서 띄움)
// =============================================================================
// [담당] Core Gameplay (윤호영)
// [역할] 인벤토리에 있는 웨건 목록을 버튼으로 만들고, 하나 고르면 콜백으로 돌려준다.
//        웨건 정보 타입은 TransportSelectPanel.TransportEntry를 그대로 사용(중복 정의 방지).
//        순수 UI — 어떤 웨건이 소지됐는지 등은 호출하는 쪽이 목록으로 넘긴다.
// =============================================================================

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>웨건 선택 팝업 — 인벤토리 웨건 목록 + 선택 콜백. [1차 빌드]</summary>
public class WagonSelectPopup : MonoBehaviour
{
    [Header("목록")]
    [SerializeField] private Transform listContainer;   // 웨건 버튼이 담기는 부모
    [SerializeField] private Button buttonPrefab;       // 웨건 버튼 프리팹 (자식 TMP_Text)

    [Header("버튼")]
    [SerializeField] private Button cancelButton;       // 닫기

    private readonly List<Button> spawned = new List<Button>();
    private Action<TransportSelectPanel.TransportEntry> onSelect;
    private bool wired;

    private void EnsureWired()
    {
        if (wired) return;
        wired = true;
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
    }

    /// <summary>팝업을 연다. wagons = 고를 수 있는 웨건 목록, onSelect = 선택 콜백.</summary>
    public void Open(IReadOnlyList<TransportSelectPanel.TransportEntry> wagons,
                     Action<TransportSelectPanel.TransportEntry> onSelect)
    {
        EnsureWired();
        this.onSelect = onSelect;
        Rebuild(wagons);
        gameObject.SetActive(true);
    }

    /// <summary>웨건 버튼 목록을 다시 만든다.</summary>
    private void Rebuild(IReadOnlyList<TransportSelectPanel.TransportEntry> wagons)
    {
        ClearButtons();
        if (listContainer == null || buttonPrefab == null || wagons == null) return;

        foreach (TransportSelectPanel.TransportEntry w in wagons)
        {
            Button b = Instantiate(buttonPrefab, listContainer);
            TMP_Text t = b.GetComponentInChildren<TMP_Text>();
            if (t != null)
                t.text = $"{w.name}  [{w.type}]  slots {w.slotCount}" +
                         (w.type == TransportType.Wagon ? $"  animals {w.minAnimals}~{w.maxAnimals}" : "");
            TransportSelectPanel.TransportEntry captured = w;   // 캡처 방지
            b.onClick.AddListener(() => Choose(captured));
            spawned.Add(b);
        }
    }

    /// <summary>웨건 선택 → 콜백 + 닫기.</summary>
    private void Choose(TransportSelectPanel.TransportEntry w)
    {
        Action<TransportSelectPanel.TransportEntry> cb = onSelect;
        onSelect = null;
        gameObject.SetActive(false);
        cb?.Invoke(w);
    }

    /// <summary>취소로 닫기.</summary>
    public void Close()
    {
        onSelect = null;
        gameObject.SetActive(false);
    }

    private void ClearButtons()
    {
        foreach (Button b in spawned)
            if (b != null) Destroy(b.gameObject);
        spawned.Clear();
    }
}
