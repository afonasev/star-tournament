Shader "StarTournament/RifleDisplay"
{
 Properties
 {
  _Intensity("Display luminance",Float)=1
  _Fill("Remaining fraction",Float)=1
  _Pulse("Digit response",Float)=0
  _BarColor("Remaining color",Color)=(.45,1,.015,1)
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
   CBUFFER_START(UnityPerMaterial)
   float _Intensity,_DigitCount,_Fill,_Pulse; float4 _BarColor; float4 _DigitsA,_DigitsB,_DigitsC;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   Varyings vert(Attributes a){Varyings o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;return o;}
   float digitInk(float2 uv,int digit,float aa)
   {
    // Seven-segment artwork uses no atlas/sample and stays sharp under FPS perspective.
    int bits=digit==0?63:digit==1?6:digit==2?91:digit==3?79:digit==4?102:digit==5?109:digit==6?125:digit==7?7:digit==8?127:111;
    float ink=0;
    for(int s=0;s<7;s++)
    {
     float2 center=s==0?float2(.5,.90):s==1?float2(.82,.70):s==2?float2(.82,.30):s==3?float2(.5,.10):s==4?float2(.18,.30):s==5?float2(.18,.70):float2(.5,.50);
     float2 size=(s==0||s==3||s==6)?float2(.25,.065):float2(.065,.15);
     float2 q=abs(uv-center);
     float d=max(max(q.x-size.x,q.y-size.y),(q.x+q.y-size.x-size.y+.035)*.7071);
     ink=max(ink,(1-smoothstep(-aa,aa,d))*((bits>>s)&1));
    }
    return ink;
   }
   half4 frag(Varyings i):SV_Target
   {
    if(i.uv.x<0)return half4(.022,.03,.04,1);
    float2 p=abs(i.uv-.5)*2;
    float edge=max(max(p.x,p.y),(p.x+p.y)/1.9);
    float aa=max(fwidth(edge),.001);
    float rim=smoothstep(.80-aa,.80+aa,edge);
    float highlight=exp(-abs(edge-.90)*100)*.22;
    float3 glass=float3(.004,.008,.012)+pow(saturate(i.uv.y-i.uv.x-.3),5)*.08;
    float3 color=lerp(glass,float3(.05,.065,.08)+highlight,rim);
    color=lerp(color,float3(.012,.018,.025),smoothstep(.97,1,edge));
    // Digits expand within the dedicated right-hand cell, including hundreds/over-capacity.
    float2 textUV=(i.uv-float2(.69,.22))/float2(.20,.56);
    float cell=textUV.x*_DigitCount;
    int index=clamp((int)floor(cell),0,9);
    float digit=index<4?_DigitsA[index]:index<8?_DigitsB[index-4]:_DigitsC[index-8];
    float digitAA=max(max(fwidth(cell),fwidth(textUV.y))*.6,.001);
    float ink=digitInk(float2(frac(cell),textUV.y),(int)digit,digitAA);
    ink*=step(0,textUV.x)*step(textUV.x,1)*step(0,textUV.y)*step(textUV.y,1);
    color=lerp(color,float3(.45,1,.015)*_Intensity*(1+_Pulse),ink*(1-rim));
    // Ten fixed art slots. Remaining light grows from the number towards the muzzle.
    float2 bar=(i.uv-float2(.065,.33))/float2(.59,.34);
    float slot=bar.x*10;
    float2 shape=abs(float2(frac(slot),bar.y)-.5)*2;
    float box=max(shape.x/.76,shape.y);
    float mask=(1-smoothstep(.90,1,box))*step(0,bar.x)*step(bar.x,1)*step(0,bar.y)*step(bar.y,1);
    float lit=saturate(_Fill*10-(9-floor(slot)));
    float3 segment=lerp(float3(.022,.035,.04),_BarColor.rgb*_Intensity,lit);
    color=lerp(color,segment,mask*(1-rim));
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}
