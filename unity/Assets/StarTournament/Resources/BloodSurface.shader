Shader "StarTournament/BloodSurface"
{
    Properties { _BaseColor("Color",Color)=(0.36,0.025,0.035,1) _Opacity("Opacity",Range(0,1))=0.9 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings Vert(Attributes input) { Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);return output; }
            half4 Frag(Varyings input):SV_Target { return half4(_BaseColor.rgb,_Opacity); }
            ENDHLSL
        }
    }
}
