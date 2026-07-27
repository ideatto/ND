// =============================================================================
// CurrentTownNameLabel — 상단 중앙 이름표: 지금 보고 있는 마을 이름 표시
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 무역마을 화면이 켜져 있으면 그 무역마을 이름을, 아니면 거점 이름을 표시한다.
//        무역마을 이름은 SharedGameData의 DisplayName을 쓰고, 없으면 townId로 대체.
//
// [부착] 상단 중앙 이름표(TMP_Text)에 붙인다. TradeTownView / MinimapTownClickRouter 참조.
// =============================================================================

using UnityEngine;
using TMPro;

/// <summary>현재 표시 중인 마을 이름을 상단 이름표에 보여준다.</summary>
public class CurrentTownNameLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text label;                       // 이름을 쓸 텍스트(비면 자기 자신)
    [SerializeField] private TradeTownView tradeTownView;          // 무역마을 화면(표시 중인지 확인)
    [SerializeField] private MinimapTownClickRouter router;        // 현재 진입한 무역마을 id
    [SerializeField] private string homeName = "거점";             // 거점 화면일 때 표시할 이름

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
    }

    private void LateUpdate()
    {
        if (label == null) return;

        bool inTradeTown = tradeTownView != null && tradeTownView.IsShowing;
        if (inTradeTown && router != null)
            label.text = ResolveTownName(router.CurrentTownId);
        else
            label.text = homeName;
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
