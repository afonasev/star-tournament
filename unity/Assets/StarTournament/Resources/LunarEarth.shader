Shader "StarTournament/LunarEarth"
{
    Properties { _BaseMap("NASA Blue Marble", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; float2 uv:TEXCOORD2; };
            V vert(A a) { V o; o.positionWS=TransformObjectToWorld(a.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(a.normalOS); o.uv=a.uv; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) { float2 i=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y); }
            half4 frag(V i):SV_Target
            {
                float3 n=normalize(i.normalWS),v=normalize(_WorldSpaceCameraPos-i.positionWS);
                float2 uv=i.uv; uv.x=frac(uv.x+.18);
                float3 land=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                float2 q=uv*float2(36,24); q.x+=sin(q.y*.65)*2;
                float clouds=noise(q)*.55+noise(q*2.1)*.28+noise(q*4.2)*.17;
                clouds=smoothstep(.49,.7,clouds)*.87;
                float light=smoothstep(-.18,.8,dot(n,normalize(float3(.7,.45,-.6))));
                float3 color=lerp(land,float3(.91,.95,1),clouds)*(.075+light*.95);
                float rim=pow(1-saturate(dot(n,v)),4);
                color+=float3(.08,.35,.7)*rim*(.2+light*.8);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
