// =============================================================================
// ND/CurvedWorld — 커브드 월드 URP Unlit 셰이더 (트레드밀 전용)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계
//
// [출처] 사내 참고 트레드밀 프로젝트(road)의 RoadGame/CurvedUnlit을 ND용으로 이식.
// [원리] 먼 정점을 뷰 공간에서 아래로 휘게 해(viewPos.y -= curvature·z²) 지평선이 둥글게
//        말리는 "Path of Kings"식 느낌 + 안개 원근. 트레드밀 길이 멀리서 아래로 굽어 보인다.
// [색] [MainColor] _BaseColor로 두어 MaterialPropertyBlock의 _BaseColor(지형색)가 그대로 먹는다.
//      Unlit이라 조명 영향 없이 평평한 색(임시 지형 타일에 적합). 이후 실제 텍스처로 교체 가능.
// =============================================================================
Shader "ND/CurvedWorld"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor]   _BaseColor ("Color", Color) = (1,1,1,1)
        _Curvature ("Curvature", Float) = 0.0025
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
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float fogCoord : TEXCOORD1; };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Curvature;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 viewPos = TransformWorldToView(worldPos);
                viewPos.y -= _Curvature * (viewPos.z * viewPos.z);   // 먼 곳일수록 아래로 휨
                OUT.positionHCS = TransformWViewToHClip(viewPos);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                c.rgb = MixFog(c.rgb, IN.fogCoord);
                return c;
            }
            ENDHLSL
        }
    }
}
