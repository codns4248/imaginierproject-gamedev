// 스프라이트의 알파(투명도)만 그대로 쓰고, 색상은 무조건 _Color로 덮어써서
// 텍스처 원본 색과 상관없이 완전한 단색 실루엣으로 그려주는 셰이더.
// (기본 Sprites-Default는 텍스처 색에 _Color를 "곱하기"만 해서 흰색을 줘도 원본 색이 그대로 나온다 -
// 이 프로젝트는 URP를 쓰므로 URP 호환 HLSL로 작성했다.)
Shader "Custom/SpriteSolidColor"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Color;

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                return float4(_Color.rgb, alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
