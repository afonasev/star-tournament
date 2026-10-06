Shader "StarTournament/OrbitalSurface"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            // Architectural text respects walls; never uses the overlay UI font shader.
            ZTest LEqual
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float _Cutoff;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;output.color=input.color;return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 color=_Color*input.color;
                // Legacy font atlases carry glyph coverage in alpha; RGB can be black.
                color.a*=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a;
                clip(color.a-_Cutoff);return color;
            }
            ENDHLSL
        }
    }
}
