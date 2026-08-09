using System;
using UnityEngine;

namespace ND.Audio
{
    [Serializable]
    public sealed class SoundDefinition
    {
        [Tooltip("사운드를 조회할 때 사용하는 안정적인 문자열 ID입니다.")]
        [SerializeField] private string id;
        [SerializeField] private SoundCategory category;
        [Tooltip("재생 시 무작위로 선택할 AudioClip 후보입니다.")]
        [SerializeField] private AudioClip[] clips;
        [Tooltip("채널 볼륨과 별도로 적용되는 개별 사운드 gain입니다. 범위는 0~1입니다.")]
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [Tooltip("무작위 pitch 최솟값입니다. 범위는 0.1~3입니다.")]
        [SerializeField, Range(0.1f, 3f)] private float minPitch = 1f;
        [Tooltip("무작위 pitch 최댓값입니다. 범위는 0.1~3입니다.")]
        [SerializeField, Range(0.1f, 3f)] private float maxPitch = 1f;

        public string Id => id;
        public SoundCategory Category => category;
        public AudioClip[] Clips => clips;
        public float Volume => Mathf.Clamp01(volume);
        public float MinPitch => Mathf.Clamp(minPitch, 0.1f, 3f);
        public float MaxPitch => Mathf.Clamp(maxPitch, 0.1f, 3f);

        internal void NormalizePitchRange()
        {
            if (float.IsNaN(minPitch) || float.IsInfinity(minPitch)) minPitch = 1f;
            if (float.IsNaN(maxPitch) || float.IsInfinity(maxPitch)) maxPitch = 1f;
            minPitch = Mathf.Clamp(minPitch, 0.1f, 3f);
            maxPitch = Mathf.Clamp(maxPitch, 0.1f, 3f);
            if (minPitch <= maxPitch) return;

            float previousMin = minPitch;
            minPitch = maxPitch;
            maxPitch = previousMin;
        }
    }
}
