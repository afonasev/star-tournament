Shader "StarTournament/QuarrySurface"
{
    Properties { [MainColor] _BaseColor("Color",Color)=(1,1,1,1) _Mode("Surface",Float)=0 _StoneCell("Stone cell",Float)=2.8 _BaseMap("Base",2D)="white"{} _Cutoff("Cutoff",Float)=0.5 _Cull("Cull",Float)=2 }
    SubShader
    {
        Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;float _Mode;float _StoneCell;float4 _BaseMap_ST;float _Cutoff;float _Cull;
            CBUFFER_END
            struct A{float4 p:POSITION;float3 n:NORMAL;};
            struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
            V Vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
            float H(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float N(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(H(i),H(i+float2(1,0)),f.x),lerp(H(i+float2(0,1)),H(i+1),f.x),f.y);}
            half4 Frag(V v):SV_Target
            {
                float3 normal=normalize(v.n),an=abs(normal);float2 uv=an.y>.6?v.w.xz:an.x>an.z?v.w.zy:v.w.xy;
                float3 color=_BaseColor.rgb;
                if(_Mode<1.5)
                {
                    float coarse=N(uv/_StoneCell),grain=N(uv*5);
                    float strata=pow(abs(sin(uv.y*1.8+N(uv*.45)*5)),24);
                    color*=.76+coarse*.32+grain*.12;color*=1-strata*.13;
                }
                else if(_Mode<2.5)color*=.82+N(uv*.7)*.2+N(uv*15)*.1;
                else
                {
                    float2 q=uv/float2(2,3);float2 edge=min(frac(q),1-frac(q));float seam=1-smoothstep(.002,.008,min(edge.x,edge.y));
                    color*=.87+H(floor(q))*.15;color*=1-seam*.65;
                    float bolt=1-smoothstep(.012,.024,length(edge-float2(.05,.05)));color=lerp(color,float3(.15,.17,.18),bolt);
                }
                Light l=GetMainLight(TransformWorldToShadowCoord(v.w));float3 light=SampleSH(normal)+l.color*(.12+.88*saturate(dot(normal,l.direction)))*l.shadowAttenuation;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                for(uint i=0;i<GetAdditionalLightsCount();i++){Light a=GetAdditionalLight(i,v.w);light+=a.color*saturate(dot(normal,a.direction))*a.distanceAttenuation*a.shadowAttenuation;}
                #endif
                return half4(color*light,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
