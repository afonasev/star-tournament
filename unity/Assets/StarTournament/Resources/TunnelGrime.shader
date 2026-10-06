Shader "StarTournament/TunnelGrime"
{
    Properties { _Opacity("Amount",Range(0,1))=.35 }
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
            float _Opacity;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            V Vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
            half4 Frag(V i):SV_Target
            {
                float2 q=float2(i.world.x+i.world.z*.71,i.world.z+i.world.y*2.7);
                float n=noise(q*2)*.55+noise(q*11)*.3+noise(q*45)*.15;
                float a=smoothstep(.4,.7,n)*saturate((.5-i.world.y)*1.5)*_Opacity;
                return half4(lerp(half3(.09,.075,.035),half3(.16,.20,.09),n),a);
            }
            ENDHLSL
        }
    }
}
