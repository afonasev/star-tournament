Shader "StarTournament/PlasmaCorona"
{
 Properties { _Tint("Plasma green",Color)=(.14,1,.025,.7) _Phase("Match phase",Float)=0 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Blend SrcAlpha One
   ZWrite Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
   struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 world:TEXCOORD1; float3 local:TEXCOORD2; };
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint; float _Phase;
   CBUFFER_END
   Varyings vert(Attributes a)
   {
    Varyings o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
    o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.local=a.normalOS;return o;
   }
   half4 frag(Varyings i):SV_Target
   {
    // Authored surface pattern coefficients only: no simulation state, lights or collision volumes.
    float face=saturate(dot(normalize(i.normalWS),normalize(GetCameraPositionWS()-i.world)));
    float wave=sin(dot(i.local,float3(12,9,7))+sin(dot(i.local,float3(4,-7,5))+_Phase));
    float filament=pow(saturate(1-abs(wave)),8);
    float envelope=smoothstep(0,.55,face);
    return half4(lerp(_Tint.rgb,half3(.65,1,.32),filament),_Tint.a*envelope*(.2+.8*filament));
   }
   ENDHLSL
  }
 }
}
