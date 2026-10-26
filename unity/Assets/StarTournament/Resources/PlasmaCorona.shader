Shader "StarTournament/PlasmaCorona"
{
 Properties { _Tint("Plasma green",Color)=(.14,1,.025,.7) _Phase("Match phase",Float)=0 _Filaments("Filament weight",Float)=1 }
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
   half4 _Tint; float _Phase; float _Filaments;
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
    float3 q=normalize(i.local);
    float wave=sin(dot(q,float3(13,9,7))+sin(dot(q,float3(4,-7,5))+_Phase*1.7));
    float secondary=sin(dot(q,float3(-8,17,11))+_Phase*2.3+wave*1.9);
    float filament=pow(saturate(1-abs(wave)*2),5);
    float branches=pow(saturate(1-abs(secondary)*3),7)*.55;
    float cloud=.5+.5*sin(dot(q,float3(5,7,-4))+_Phase+secondary*.4);
    // Object-space radial fade also remains soft when a contact lens is flattened.
    float radial=saturate(dot(normalize(i.local),normalize(TransformWorldToObjectDir(GetCameraPositionWS()-i.world))));
    float envelope=pow(radial,2.5);
    float energy=saturate(filament+branches)*_Filaments;
    return half4(lerp(_Tint.rgb,half3(.72,1,.39),energy),_Tint.a*envelope*(.38+cloud*.2+energy*.42));
   }
   ENDHLSL
  }
 }
}
