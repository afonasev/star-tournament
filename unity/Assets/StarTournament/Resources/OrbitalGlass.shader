Shader "StarTournament/OrbitalGlass"
{
    Properties
    {
        _Color ("Tint and transmission opacity", Color) = (0.35,0.65,0.8,0.045)
        _EdgeOpacity ("Grazing opacity", Range(0,1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;float _EdgeOpacity;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(input.normalOS);return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float facing=abs(dot(normalize(input.normalWS),normalize(GetWorldSpaceViewDir(input.positionWS))));
                return half4(_Color.rgb,lerp(_Color.a,_EdgeOpacity,(1-facing)*(1-facing)));
            }
            ENDHLSL
        }
    }
}
