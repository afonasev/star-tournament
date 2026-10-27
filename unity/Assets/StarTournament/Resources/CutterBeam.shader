Shader "StarTournament/CutterBeam"
{
 Properties { _Tint("Beam color",Color)=(1,1,1,1) _NearFade("Camera clearance",Float)=0.5 _Radial("Radial contact",Float)=0 }
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
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; half4 color:COLOR; };
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint; float _NearFade; float _Radial;
   CBUFFER_END
   Varyings vert(Attributes a){Varyings o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=a.uv;o.color=a.color;return o;}
   half4 frag(Varyings i):SV_Target
   {
    // Smooth envelope over one clearance-length. This is view-only clipping, not a hit-volume change.
    float near=smoothstep(_NearFade,_NearFade+_NearFade,distance(i.world,GetCameraPositionWS()));
    float edge=lerp(saturate(1-abs(i.uv.y*2-1)),pow(saturate(1-length(i.uv*2-1)),2),_Radial*i.color.r);
    // Contact vertex G carries hot-core whiteness; ordinary LineRenderers keep their material tint.
    edge=lerp(1,edge,i.color.b);
    half3 tint=lerp(_Tint.rgb,max(_Tint.rgb,1.0),_Radial*i.color.g);
    return half4(tint,_Tint.a*near*edge*i.color.a);
   }
   ENDHLSL
  }
 }
}
