Shader "StarTournament/CutterBeam"
{
 Properties { _Tint("Beam color",Color)=(1,1,1,1) _NearFade("Camera clearance",Float)=0.5 }
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
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint; float _NearFade;
   CBUFFER_END
   Varyings vert(Attributes a){Varyings o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=a.uv;return o;}
   half4 frag(Varyings i):SV_Target
   {
    // Smooth envelope over one clearance-length. This is view-only clipping, not a hit-volume change.
    float near=smoothstep(_NearFade,_NearFade+_NearFade,distance(i.world,GetCameraPositionWS()));
    float edge=saturate(1-abs(i.uv.y*2-1));
    return half4(_Tint.rgb,_Tint.a*near*edge);
   }
   ENDHLSL
  }
 }
}
