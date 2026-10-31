Shader "StarTournament/ShotgunDisplay"
{
 Properties
 {
  _MainTex("Authored digit atlas",2D)="black"{}
  _Intensity("Display luminance",Float)=1
  _DigitCount("Digit count",Float)=2
  _DigitsA("Digits 0..3",Vector)=(0,0,0,0)
  _DigitsB("Digits 4..7",Vector)=(0,0,0,0)
  _DigitsC("Digits 8..9",Vector)=(0,0,0,0)
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   ZWrite On
   Cull Back
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float _Intensity,_DigitCount; float4 _DigitsA,_DigitsB,_DigitsC;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   Varyings vert(Attributes a){Varyings o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;return o;}
   half4 frag(Varyings i):SV_Target
   {
    // Normalized artwork: clipped glass, machined double bevel and a quiet reflection.
    float2 p=abs(i.uv-.5)*2;
    float edge=max(max(p.x,p.y),(p.x+p.y)/1.8);
    float aa=max(fwidth(edge),.001);
    float rim=smoothstep(.83-aa,.83+aa,edge);
    float highlight=exp(-abs(edge-.905)*100)*.22;
    float3 glass=float3(.006,.012,.016)+pow(saturate(i.uv.y-i.uv.x-.25),5)*.12;
    float3 color=lerp(glass,float3(.052,.065,.075)+highlight,rim);
    color=lerp(color,float3(.018,.024,.029),smoothstep(.96,1,edge));
    // Decimal cells use the full integer string (minimum two digits), not a modulo-100 counter.
    float2 textUV=(i.uv-float2(.17,.24))/float2(.66,.60);
    float cell=textUV.x*_DigitCount;
    int index=clamp((int)floor(cell),0,9);
    float digit=index<4?_DigitsA[index]:index<8?_DigitsB[index-4]:_DigitsC[index-8];
    float ink=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2((digit+frac(cell))*.1,textUV.y)).r;
    ink*=step(0,textUV.x)*step(textUV.x,1)*step(0,textUV.y)*step(textUV.y,1);
    float underline=(1-smoothstep(.008,.008+fwidth(i.uv.y),abs(i.uv.y-.205)))*step(.19,i.uv.x)*step(i.uv.x,.81);
    color=lerp(color,float3(.55,.94,1)*_Intensity,ink*(1-rim));
    color=lerp(color,float3(.10,.80,.92)*_Intensity,underline*(1-rim));
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}
