Shader "StarTournament/PulseSoftParticle"
{
 Properties
 {
  _MainTex("Pulse cloud",2D)="white"{}
  _FadeDistance("Surface fade (metres)",Float)=0.3
  _Radiance("Fire radiance",Float)=1
 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float eyeDepth:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float _FadeDistance; float _Radiance;
   CBUFFER_END
   Varyings vert(Attributes a)
   {
    Varyings o; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 world=TransformObjectToWorld(a.positionOS.xyz);
    o.positionCS=TransformWorldToHClip(world);o.eyeDepth=-TransformWorldToView(world).z;
    o.uv=a.uv;o.color=a.color;return o;
   }
   half4 frag(Varyings i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
    float scene=unity_OrthoParams.w==0?LinearEyeDepth(raw,_ZBufferParams):LinearDepthToEyeDepth(raw);
    half4 cloud=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
    cloud.a*=saturate((scene-i.eyeDepth)/max(_FadeDistance,0.0001));
    cloud.rgb*=_Radiance;return cloud;
   }
   ENDHLSL
  }
 }
}
