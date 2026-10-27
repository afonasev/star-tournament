Shader "StarTournament/CutterBattery"
{
 Properties { _ChargedColor("Charged cells",Color)=(1,0.36,0.035,1) _DarkColor("Empty cells",Color)=(0.012,0.012,0.012,1) _Charge("Energy fraction",Range(0,1))=0 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   ZWrite On
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
   struct Varyings { float4 positionCS:SV_POSITION; float cell:TEXCOORD0; };
   CBUFFER_START(UnityPerMaterial)
   half4 _ChargedColor; half4 _DarkColor; float _Charge;
   CBUFFER_END
   Varyings vert(Attributes a){Varyings o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.cell=a.uv.x;return o;}
   half4 frag(Varyings i):SV_Target
   {
    // Authored topology invariant: UV.x is 0..15 from bottom to top, constant per quad.
    // One partially charged cell fades continuously; no time-based simulation in the shader.
    float fill=saturate(saturate(_Charge)*16.0-i.cell);
    return half4(lerp(_DarkColor.rgb,_ChargedColor.rgb,fill),1);
   }
   ENDHLSL
  }
 }
}
