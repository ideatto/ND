using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Loading
{
    [CreateAssetMenu(fileName = "LoadingTipDatabase", menuName = "ND/Loading/Tip Database")]
    public sealed class LoadingTipDatabase : ScriptableObject
    {
        [SerializeField] private List<string> tips = new List<string>();
        [SerializeField] private string fallbackTip = "여행 준비를 하고 있습니다…";

        public IReadOnlyList<string> Tips => tips;
        public string FallbackTip => fallbackTip;
    }
}
