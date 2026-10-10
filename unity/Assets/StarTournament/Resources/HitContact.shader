Shader "StarTournament/HitContact"
{
    Properties { _Color("Color",Color)=(0.36,0.025,0.035,1) _Opacity("Opacity",Range(0,1))=1 _Shield("Shield",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Color; half _Opacity; half _Shield;
            CBUFFER_END
            Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o;}
            half4 frag(Varyings input):SV_Target
            {
                float2 p=input.uv;float radius=length(p);float angle=atan2(p.y,p.x);
                // Fixed silhouette basis gives an asymmetric stain; size/lifetime/color are profile-controlled.
                float edge=.77+.10*sin(angle*3+.6)+.07*sin(angle*7+1.4);
                float blood=1-smoothstep(edge-.08,edge,radius);
                float shield=(1-smoothstep(.65,1,radius))*(.45+.55*(1-smoothstep(0,.5,radius)));
                float alpha=lerp(blood,shield,_Shield)*_Opacity;clip(alpha-.005);
                return half4(_Color.rgb,alpha*_Color.a);
            }
            ENDHLSL
        }
    }
}
