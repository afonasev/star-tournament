Shader "StarTournament/PlasmaFilament"
{
 Properties { _Tint("Ion color",Color)=(.3,1,.08,.75) }
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
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint;
   CBUFFER_END
   Varyings vert(Attributes a){Varyings o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color;return o;}
   half4 frag(Varyings i):SV_Target
   {
    // LineRenderer supplies longitudinal vertex alpha. Feather its transverse ribbon too.
    float feather=pow(saturate(1-abs(i.uv.y*2-1)),2.2);
    return half4(_Tint.rgb*i.color.rgb,_Tint.a*i.color.a*feather);
   }
   ENDHLSL
  }
 }
}
