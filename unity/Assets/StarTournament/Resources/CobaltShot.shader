Shader "StarTournament/CobaltShot"
{
 Properties { _MainTex("Authored envelope",2D)="white"{} _Radiance("Radiance",Float)=1 _Tracer("Tracer",Float)=0 _Core("Core",Range(0,1))=.8 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Blend SrcAlpha One
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float _Radiance,_Tracer,_Core;
   CBUFFER_END
   V vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color;return o;}
   half4 frag(V i):SV_Target
   {
    if(_Tracer>.5)
    {
     // UV x follows the finite segment head-to-tail; never draw a muzzle-to-hit beam.
     float across=abs(i.uv.y*2-1),tail=1-saturate(i.uv.x);
     float alpha=pow(saturate(1-across),1.5)*smoothstep(0,.7,tail);
     float core=pow(saturate(1-across*3),2)*_Core;
     return half4(lerp(i.color.rgb,half3(1,.96,.86),core)*_Radiance,alpha*i.color.a);
    }
    half4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
    return half4(tex.rgb*i.color.rgb*_Radiance,tex.a*i.color.a);
   }
   ENDHLSL
  }
 }
}
