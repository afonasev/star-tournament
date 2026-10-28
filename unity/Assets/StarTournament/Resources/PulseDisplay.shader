Shader "StarTournament/PulseDisplay"
{
 Properties
 {
  _MainTex("Authored digit atlas",2D)="black"{}
  _Intensity("Digit luminance",Float)=1
  _ArcIntensity("Reserve arc luminance",Float)=.8
  _DigitScale("Digit scale",Float)=1
  _DigitCount("Digit count",Float)=2
  _DigitsA("Digits 0..3",Vector)=(0,0,0,0)
  _DigitsB("Digits 4..7",Vector)=(0,0,0,0)
  _DigitsC("Digits 8..9",Vector)=(0,0,0,0)
  _Reserve("Reserve fraction",Float)=0
  _Empty("Empty reserve",Float)=0
 }
 SubShader
 {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
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
   float _Intensity,_ArcIntensity,_DigitScale,_DigitCount,_Reserve,_Empty;
   float4 _DigitsA,_DigitsB,_DigitsC;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   Varyings vert(Attributes a){Varyings o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;return o;}
   float band(float value,float center,float width,float aa)
   {return 1-smoothstep(width,width+aa,abs(value-center));}
   half4 frag(Varyings i):SV_Target
   {
    // Fixed normalized artwork. Profile controls the actual size, placement and luminance.
    float2 p=(i.uv-.5)*2;float radius=length(p);float aa=max(fwidth(radius),.002);
    float3 color=float3(.008,.010,.012)+pow(saturate(i.uv.y-i.uv.x+.1),4)*.025;
    float bevel=band(radius,.965,.018,aa);
    color+=bevel*(.07+.10*saturate(-p.x+p.y));
    color+=band(radius,.91,.004,aa)*.045;
    // A quiet segmented quarter-dial, matching C. Full arc denotes configured reserve.
    float angle=atan2(p.x,p.y);
    float arcT=(angle+.20)/2.05;
    float arcRegion=step(0,arcT)*step(arcT,1)*band(radius,.805,.037,aa);
    float cells=arcT*12;
    float gaps=smoothstep(.06,.14,frac(cells))*(1-smoothstep(.85,.95,frac(cells)));
    float active=step((floor(cells)+.5)/12,_Reserve)*(1-_Empty);
    color=lerp(color,lerp(float3(.06,.041,.019),float3(1,.59,.16)*_ArcIntensity,active),arcRegion*gaps);
    // Full integer string, at least two digits; never modulo 100.
    float2 textSize=float2(.62,.68)*_DigitScale;
    float2 textUV=(i.uv-float2(.5-textSize.x*.5,.56-textSize.y*.5))/textSize;
    float cell=textUV.x*_DigitCount;int index=clamp((int)floor(cell),0,9);
    float digit=index<4?_DigitsA[index]:index<8?_DigitsB[index-4]:_DigitsC[index-8];
    float ink=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2((digit+frac(cell))*.1,textUV.y)).r;
    ink*=step(0,textUV.x)*step(textUV.x,1)*step(0,textUV.y)*step(textUV.y,1);
    float3 light=lerp(float3(1,.91,.72),float3(1,.055,.025),_Empty)*_Intensity;
    color=lerp(color,light,ink);
    // Small authored rocket silhouette, central porthole and short engraved guide strokes.
    float2 q=float2(abs(p.x),p.y+.52);
    float hullWidth=.036*sqrt(saturate(1-pow((q.y-.015)/.15,2)));
    float hull=(1-smoothstep(hullWidth,hullWidth+aa,q.x))*step(-.12,q.y)*step(q.y,.16);
    float fins=(1-smoothstep(.085,.085+aa,q.x))*step(-.12,q.y)*step(q.y,-.04-q.x*.6);
    float flame=(1-smoothstep(.026,.026+aa,q.x))*step(-.18,q.y)*step(q.y,-.135);
    float icon=saturate(hull+fins+flame);
    float port=1-smoothstep(.014,.014+aa,length(float2(p.x,q.y-.025)));
    color=lerp(color,light*.8,icon);color=lerp(color,float3(.01,.012,.014),port);
    float guides=band(p.y,-.52,.003,aa)*step(.13,abs(p.x))*step(abs(p.x),.26);
    color=lerp(color,light*.45,guides);
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}
