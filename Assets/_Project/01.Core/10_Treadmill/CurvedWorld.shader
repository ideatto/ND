// =============================================================================
// ND/CurvedWorld — 커브드 월드 URP Unlit + 지형 2텍스처 노이즈 블렌드 (트레드밀 전용)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계
//
// [원리] 먼 정점을 뷰공간에서 아래로 휘게(viewPos.y -= curvature·z²) → 둥근 지평선 + 안개.
//        바닥은 _BaseMap(예: 풀)과 _DetailMap(예: 흙)을 절차적 노이즈로 섞어, 한 텍스처만
//        깔린 "양탄자" 느낌 대신 풀/흙이 랜덤하게 얼룩진 자연스러운 지면을 만든다.
//
// [이음매 없음의 핵심] 텍스처 UV와 흙 노이즈를 '타일(길 조각)의 메시 UV'가 아니라
//        **월드 좌표(worldPos.xz) + 스크롤(_ScrollZ)**로 직접 계산한다. 그래서 길이 여러
//        조각으로 나뉘어 있어도 텍스처·얼룩은 하나의 연속된 지면처럼 흐르고, 조각 경계에서
//        UV가 리셋되지 않아 '타일과 타일 사이 이음매'가 원천적으로 사라진다. (_ScrollZ를
//        길 진행거리로 매 프레임 갱신하면 지면이 길과 함께 스크롤된다.)
// =============================================================================
Shader "ND/CurvedWorld"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base (바닥)", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1,1,1,1)
        _DetailMap ("Detail (섞을 텍스처)", 2D) = "white" {}
        _DetailColor ("Detail Color", Color) = (1,1,1,1)
        _WorldTexScale ("World Tex Scale (1/m)", Float) = 0.16    // 텍스처 반복: 1/타일미터
        _NoiseWorldScale ("Noise World Scale (1/m)", Float) = 0.35 // 흙 얼룩 빈도: 클수록 작은 얼룩
        _NoiseAmount ("Detail Amount", Range(0,1)) = 0.3
        _DetailSoftness ("Detail Edge Softness", Range(0.02,0.4)) = 0.16
        _ScrollZ ("Scroll Z (m)", Float) = 0                      // 길 진행거리(지면 스크롤)
        // 지형 전환용 노이즈 디졸브: 경계(EdgeZ)~+Band 구간에서 노이즈로 구멍을 내며 사라짐
        _DissolveOn ("Dissolve On", Float) = 0
        _DissolveEdgeZ ("Dissolve Edge Z (world)", Float) = 0
        _DissolveBand ("Dissolve Band (m)", Float) = 14
        _DissolveSoft ("Dissolve Softness", Range(0.05,0.5)) = 0.35   // 클수록 흐릿(부드러운 알파)
        _DissolveNoise ("Dissolve Noise Distort", Range(0,1.5)) = 0.8 // 경계 노이즈 왜곡량
        // 자체 거리 안개(전역 RenderSettings 대신 → 다른 카메라엔 영향 없음)
        _FogColor ("Fog Color", Color) = (0.72,0.78,0.8,1)
        _FogStart ("Fog Start (m)", Float) = 16
        _FogEnd ("Fog End (m)", Float) = 46
        _Curvature ("Curvature", Float) = 0
        _ShadowTint ("Shadow Tint (그림자 밝기, 낮을수록 진함)", Range(0,1)) = 0.55  // 그림자 진 바닥 밝기
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 메인 라이트 그림자 수신(마차가 길에 그림자를 드리우게)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 worldUV : TEXCOORD0;   // 월드 XZ + 스크롤(텍스처/노이즈 공용, 타일 무관 연속)
                float viewDepth : TEXCOORD1;  // 카메라로부터 거리(자체 안개용)
                float worldZraw : TEXCOORD2;  // 순수 월드 Z(디졸브 경계 판정용)
                float3 worldPos : TEXCOORD3;  // 월드 좌표(그림자 좌표 계산용)
            };

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DetailMap_ST;
                half4 _BaseColor;
                half4 _DetailColor;
                float _WorldTexScale;
                float _NoiseWorldScale;
                float _NoiseAmount;
                float _DetailSoftness;
                float _ScrollZ;
                float _DissolveOn;
                float _DissolveEdgeZ;
                float _DissolveBand;
                float _DissolveSoft;
                float _DissolveNoise;
                half4 _FogColor;
                float _FogStart;
                float _FogEnd;
                float _Curvature;
                float _ShadowTint;
            CBUFFER_END

            // 값 노이즈(부드러움)
            float hash21(float2 p){ return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float a = hash21(i), b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1)), d = hash21(i + float2(1,1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 viewPos = TransformWorldToView(worldPos);
                viewPos.y -= _Curvature * (viewPos.z * viewPos.z);   // 먼 곳 아래로 휨(옵션)
                OUT.positionHCS = TransformWViewToHClip(viewPos);
                // 텍스처/노이즈 좌표 = 월드 XZ. Z에 진행거리를 더해 '길과 함께' 흐르되,
                // worldPos.z가 조각 경계에서도 연속이라 타일 이음매가 생기지 않는다.
                OUT.worldUV = float2(worldPos.x, worldPos.z + _ScrollZ);
                OUT.viewDepth = -viewPos.z;   // 카메라 전방 거리(안개 페이드용)
                OUT.worldZraw = worldPos.z;
                OUT.worldPos = worldPos;      // 그림자 좌표 계산용
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // 지형 전환 디졸브 알파: 경계(EdgeZ)~+Band에서 하드 clip 대신 '부드러운 알파'로 페이드.
                //  노이즈로 경계를 유기적으로 왜곡하고 smoothstep으로 흐리게 → 옛 지형과 뭉개지듯 겹침.
                float dissolveA = 1.0;
                if (_DissolveOn > 0.5)
                {
                    float t = saturate((IN.worldZraw - _DissolveEdgeZ) / max(0.01, _DissolveBand));
                    float dn = vnoise(IN.worldUV * (_NoiseWorldScale * 1.6) + 7.3) * 0.6
                             + vnoise(IN.worldUV * (_NoiseWorldScale * 4.0) + 2.1) * 0.4;
                    float edge = t + (dn - 0.5) * _DissolveNoise;    // 노이즈로 경계 왜곡
                    dissolveA = smoothstep(0.5 - _DissolveSoft, 0.5 + _DissolveSoft, edge);  // 부드러운 알파
                }

                // 텍스처 샘플 좌표(월드 기반) → 조각이 몇 개든 하나의 연속 지면
                float2 tuv = IN.worldUV * _WorldTexScale;
                half4 baseC   = SAMPLE_TEXTURE2D(_BaseMap,   sampler_BaseMap,   tuv) * _BaseColor;
                half4 detailC = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, tuv) * _DetailColor;

                // 흙 얼룩 마스크: 월드 좌표 절차적 노이즈(여러 옥타브) → 타일과 무관하게 랜덤·연속
                float2 np = IN.worldUV * _NoiseWorldScale;
                float n = vnoise(np) * 0.6 + vnoise(np * 2.3 + 5.2) * 0.3 + vnoise(np * 5.1 + 1.7) * 0.1;

                // 경계 폭을 화면 변화율(fwidth)로 자동 조절 → 가까이 또렷/멀리 부드럽게(밴딩 제거)
                float w = _DetailSoftness + fwidth(n) * 1.5;
                float mask = smoothstep(0.5 - w, 0.5 + w, n) * _NoiseAmount;

                half4 c = lerp(baseC, detailC, mask);

                // 메인 라이트 그림자 수신: 마차·동물이 드리운 그림자 자리를 어둡게(입체감·접지감).
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.worldPos);
                    Light mainLight = GetMainLight(shadowCoord);
                    float atten = mainLight.shadowAttenuation;
                    c.rgb *= lerp(_ShadowTint, 1.0, atten);   // 그림자 진 곳은 _ShadowTint만큼 어둡게
                #endif

                // 자체 거리 안개: 가까우면 원색, _FogStart~_FogEnd 넘어가면 _FogColor로 페이드.
                float fog = saturate((_FogEnd - IN.viewDepth) / max(0.01, _FogEnd - _FogStart));
                c.rgb = lerp(_FogColor.rgb, c.rgb, fog);
                c.a = dissolveA;   // 전환 중일 때만 <1(알파 블렌드). 평소엔 1(불투명).
                return c;
            }
            ENDHLSL
        }
    }
}
