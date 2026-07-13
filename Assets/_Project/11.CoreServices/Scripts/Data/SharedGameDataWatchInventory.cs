/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Player 빌드에서 AssetDatabase 없이 catalog drift를 검사할 수 있도록 watch SO 스냅샷을 보관한다.
 *
 * Main Features
 * - watch root에서 발견한 SO의 GUID·경로·타입·데이터 ID를 직렬화한다.
 * - 스냅샷 갱신 시점의 catalog 등록 GUID 목록을 함께 보관한다.
 *
 * Important Notes
 * - Resources 로드 이름은 SharedGameDataWatchInventory 이다.
 * - Player 빌드 전에 Editor 메뉴로 inventory를 갱신해야 신규 SO drift를 감지할 수 있다.
 * - ProjectData 미등록만 Player에서 InGame 진입을 차단하고, SandboxLegacy 미등록은 경고만 남긴다.
 */
using System;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// 공용 데이터 watch 폴더 스냅샷과 catalog 등록 GUID 목록을 담는 ScriptableObject이다.
    /// </summary>
    [CreateAssetMenu(
        fileName = ResourceName,
        menuName = "ND/Framework/Shared Game Data Watch Inventory")]
    public sealed class SharedGameDataWatchInventory : ScriptableObject
    {
        public const string ResourceName = "SharedGameDataWatchInventory";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("AssetDatabase GUID입니다.")]
            public string assetGuid;

            [Tooltip("에셋 경로입니다.")]
            public string assetPath;

            [Tooltip("SO 타입 이름입니다. 예: TradeItemData")]
            public string typeName;

            [Tooltip("데이터 ID입니다. 예: Apple, BaseCamp")]
            public string dataId;

            [Tooltip("감시 루트 종류입니다. ProjectData만 Player에서 진입을 차단합니다.")]
            public SharedGameDataWatchRootKind watchRootKind;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        [Tooltip("inventory 갱신 시점에 catalog에 등록되어 있던 에셋 GUID 목록입니다.")]
        [SerializeField] private string[] catalogRegisteredGuids = Array.Empty<string>();

        /// <summary>
        /// watch root에서 발견한 SO 스냅샷이다. 반환 배열은 내부 배열 복사본이다.
        /// </summary>
        public Entry[] Entries => entries != null ? (Entry[])entries.Clone() : Array.Empty<Entry>();

        /// <summary>
        /// catalog에 등록된 에셋 GUID 스냅샷이다. 반환 배열은 내부 배열 복사본이다.
        /// </summary>
        public string[] CatalogRegisteredGuids =>
            catalogRegisteredGuids != null
                ? (string[])catalogRegisteredGuids.Clone()
                : Array.Empty<string>();

        /// <summary>
        /// Editor refresh가 inventory 내용을 교체할 때 사용한다.
        /// </summary>
        public void ReplaceSnapshot(Entry[] nextEntries, string[] nextCatalogRegisteredGuids)
        {
            entries = nextEntries ?? Array.Empty<Entry>();
            catalogRegisteredGuids = nextCatalogRegisteredGuids ?? Array.Empty<string>();
        }
    }
}
