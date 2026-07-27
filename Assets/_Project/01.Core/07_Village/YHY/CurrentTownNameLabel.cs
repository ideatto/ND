// =============================================================================
// CurrentTownNameLabel — 상단 중앙 이름표: 지금 보고 있는 마을 이름 표시
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 카메라가 지금 비추는 마을 이름을 표시한다(거점이면 "거점").
//        마을 이름은 SharedGameData의 DisplayName을 쓰고, 없으면 townId로 대체.
//        현재 마을은 TradeTownCameraMover.RequestedTownId(카메라가 향한 마을)로 판단.
//
// [부착] 상단 중앙 이름표(TMP_Text)에 붙인다.
// =============================================================================

using UnityEngine;
using TMPro;

/// <summary>현재 카메라가 비추는 마을 이름을 상단 이름표에 보여준다.</summary>
public class CurrentTownNameLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text label;                       // 이름을 쓸 텍스트(비면 자기 자신)
    [SerializeField] private string homeTownId = "BaseCamp";       // 거점 townId
    [SerializeField] private string homeName = "거점";             // 거점일 때 표시할 이름

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
    }

    private void LateUpdate()
    {
        if (label == null) return;

        // 카메라가 향한 마을. 아직 없거나(초기) 거점이면 거점 이름.
        string townId = TradeTownCameraMover.RequestedTownId;
        if (string.IsNullOrEmpty(townId) || townId == homeTownId)
            label.text = homeName;
        else
            label.text = ResolveTownName(townId);
    }

    /// <summary>townId → 표시 이름(SharedGameData DisplayName, 없으면 townId 그대로).</summary>
    private static string ResolveTownName(string townId)
    {
        if (string.IsNullOrEmpty(townId)) return "";
        var fr = ND.Framework.FrameworkRoot.Instance;
        if (fr != null && fr.SharedGameData != null
            && fr.SharedGameData.TryGetTown(townId, out ND.Framework.SharedTownDefinition town)
            && !string.IsNullOrEmpty(town.DisplayName))
        {
            return town.DisplayName;
        }
        return townId;
    }
}
