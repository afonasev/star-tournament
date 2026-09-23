Shader "StarTournament/VectorParticle"
{
 Properties { _MainTex("Soft particle",2D)="white"{} }
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
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   Varyings vert(Attributes a){Varyings o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color;return o;}
   half4 frag(Varyings i):SV_Target{return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;}
   ENDHLSL
  }
 }
}
