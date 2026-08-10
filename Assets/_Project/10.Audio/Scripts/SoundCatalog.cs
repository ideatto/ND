using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Audio
{
    [Serializable]
    public sealed class SceneBgmMapping
    {
        [Tooltip("FrameworkEvents.SceneChanged가 전달하는 Unity Scene 이름입니다.")]
        [SerializeField] private string sceneName;
        [Tooltip("빈 값이면 해당 Scene 진입 시 BGM을 중지합니다.")]
        [SerializeField] private string bgmId;

        public string SceneName => sceneName;
        public string BgmId => bgmId;
    }

    [CreateAssetMenu(fileName = ResourceName, menuName = "ND/Audio/Sound Catalog")]
    public sealed class SoundCatalog : ScriptableObject
    {
        public const string ResourceName = "SoundCatalog";

        [SerializeField] private SoundDefinition[] definitions = Array.Empty<SoundDefinition>();
        [SerializeField] private SceneBgmMapping[] sceneBgmMappings = Array.Empty<SceneBgmMapping>();

        private Dictionary<string, SoundDefinition> definitionLookup;
        private Dictionary<string, string> sceneBgmLookup;

        public bool TryGet(string id, out SoundDefinition definition)
        {
            EnsureLookup();
            if (!string.IsNullOrEmpty(id) && definitionLookup.TryGetValue(id, out definition)) return true;

            definition = null;
            return false;
        }

        public bool TryGetSceneBgm(string sceneName, out string bgmId)
        {
            EnsureLookup();
            if (!string.IsNullOrEmpty(sceneName) && sceneBgmLookup.TryGetValue(sceneName, out bgmId)) return true;

            bgmId = null;
            return false;
        }

        private void OnEnable()
        {
            RebuildLookup(true);
        }

        private void OnValidate()
        {
            if (definitions != null)
            {
                foreach (SoundDefinition definition in definitions) definition?.NormalizePitchRange();
            }

            RebuildLookup(true);
        }

        private void EnsureLookup()
        {
            if (definitionLookup == null || sceneBgmLookup == null) RebuildLookup(true);
        }

        private void RebuildLookup(bool logValidation)
        {
            definitionLookup = new Dictionary<string, SoundDefinition>(StringComparer.Ordinal);
            sceneBgmLookup = new Dictionary<string, string>(StringComparer.Ordinal);

            if (definitions != null)
            {
                foreach (SoundDefinition definition in definitions)
                {
                    if (!TryValidateDefinition(definition, logValidation)) continue;
                    if (!definitionLookup.TryAdd(definition.Id, definition) && logValidation)
                        Debug.LogError($"[SoundCatalog] Duplicate sound ID ignored: '{definition.Id}'.", this);
                }
            }

            if (sceneBgmMappings == null) return;
            foreach (SceneBgmMapping mapping in sceneBgmMappings)
            {
                if (mapping == null || string.IsNullOrEmpty(mapping.SceneName))
                {
                    if (logValidation) Debug.LogError("[SoundCatalog] Scene mapping with an empty scene name was ignored.", this);
                    continue;
                }

                if (sceneBgmLookup.ContainsKey(mapping.SceneName))
                {
                    if (logValidation) Debug.LogError($"[SoundCatalog] Duplicate scene mapping ignored: '{mapping.SceneName}'.", this);
                    continue;
                }

                if (string.IsNullOrEmpty(mapping.BgmId))
                {
                    sceneBgmLookup.Add(mapping.SceneName, string.Empty);
                    continue;
                }
                if (!definitionLookup.TryGetValue(mapping.BgmId, out SoundDefinition definition))
                {
                    Debug.LogError($"[SoundCatalog] Scene '{mapping.SceneName}' references unknown BGM ID '{mapping.BgmId}'.", this);
                    continue;
                }
                else if (definition.Category != SoundCategory.Bgm)
                {
                    Debug.LogError($"[SoundCatalog] Scene '{mapping.SceneName}' references non-BGM ID '{mapping.BgmId}'.", this);
                    continue;
                }

                sceneBgmLookup.Add(mapping.SceneName, mapping.BgmId);
            }
        }

        private bool TryValidateDefinition(SoundDefinition definition, bool logValidation)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
            {
                if (logValidation) Debug.LogError("[SoundCatalog] Null definition or empty sound ID was ignored.", this);
                return false;
            }

            AudioClip[] clips = definition.Clips;
            if (clips == null || clips.Length == 0)
            {
                if (logValidation) Debug.LogError($"[SoundCatalog] Sound '{definition.Id}' has no clips and was ignored.", this);
                return false;
            }

            bool hasValidClip = false;
            foreach (AudioClip clip in clips)
            {
                if (clip != null) hasValidClip = true;
                else if (logValidation) Debug.LogWarning($"[SoundCatalog] Sound '{definition.Id}' contains a null clip entry.", this);
            }

            if (!hasValidClip && logValidation)
                Debug.LogError($"[SoundCatalog] Sound '{definition.Id}' has no usable clip and was ignored.", this);
            return hasValidClip;
        }
    }
}
