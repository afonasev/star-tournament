Shader "StarTournament/ForgeDisplay"
{
 Properties
 {
  _MainTex("Shared authored digits",2D)="black"{}
  _Layout("Radius / width / height / counter offset",Vector)=(.148,.12,.064,-.151)
  _Intensity("Luminance",Float)=1
  _LowFraction("Low reserve threshold",Float)=.15
  _DigitCount("Digit count",Float)=1
  _DigitsA("Digits 0..3",Vector)=(0,0,0,0)
  _DigitsB("Digits 4..7",Vector)=(0,0,0,0)
  _DigitsC("Digits 8..9",Vector)=(0,0,0,0)
  _LitSegments("Lit artwork windows",Float)=0
  _Reserve("Reserve fraction",Float)=0
  _Empty("Empty reserve",Float)=1
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
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _Layout,_DigitsA,_DigitsB,_DigitsC;
   float _Intensity,_LowFraction,_DigitCount,_LitSegments,_Reserve,_Empty;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 part:TEXCOORD1;};
   struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float part:TEXCOORD1;};
   Varyings vert(Attributes a)
   {
    Varyings o;float3 p=a.positionOS.xyz;
    if(a.part.x<.5)p.xy*=_Layout.x;
    else {p.xy*=.5*_Layout.yz;p.y+=_Layout.w;p.z-=.002;}
    o.positionCS=TransformObjectToHClip(p);o.uv=a.uv;o.part=a.part.x;return o;
   }
   half4 frag(Varyings i):SV_Target
   {
    // Fixed authored normalized artwork; physical scale/readability controls live in Lab.
    float2 p=(i.uv-.5)*2;
    float3 light=lerp(float3(.58,1,.06),float3(1,.53,.025),step(_Reserve,_LowFraction));
    light=lerp(light,float3(1,.055,.018),_Empty)*_Intensity;
    float3 color;
    if(i.part<.5)
    {
     float r=length(p),aa=max(fwidth(r),.001);
     float edge=min(r-.855,1-r);
     float metal=1-smoothstep(.008,.021,edge);
     color=lerp(float3(.012,.017,.019),float3(.22,.25,.26)*(.65+.35*saturate(p.y-p.x)),metal);
     // Three authored clamp sectors remain open: the original protective bridges stay visible.
     float turn=atan2(p.x,p.y)/6.2831853+.5;
     float sector=frac(turn*3);clip(min(sector,1-sector)-.055);
     float cell=floor(turn*3)*8+(sector-.055)/.89*8,local=frac(cell);
     float window=smoothstep(.10,.17,local)*(1-smoothstep(.83,.90,local));
     window*=smoothstep(.877,.887,r)*(1-smoothstep(.969,.979,r));
     // Deplete toward the upper-left windows, clear of the lower numeric collar.
     float ordinal=fmod(11-floor(cell)+24,24);
     float lit=(1-step(_LitSegments,ordinal))*(1-_Empty);
     float glass=smoothstep(.887,.901,r)*(1-smoothstep(.955,.969,r));
     float3 inset=lerp(float3(.024,.032,.031),light*(.65+.35*glass),lit);
     color=lerp(color,inset,window);
    }
    else
    {
     // Chamfered collar, black glass, a number only. No icon or secondary bar.
     float d=max(max(abs(p.x),abs(p.y)),(abs(p.x)+abs(p.y)*.625)/1.425);
     float edge=smoothstep(.79,.84,d)*(1-smoothstep(.97,1,d));
     float bevel=.55+.45*saturate(p.y-p.x*.4);
     color=lerp(float3(.006,.010,.012),float3(.32,.34,.34)*bevel,edge);
     color+=pow(saturate(i.uv.y-i.uv.x+.1),4)*.012;
     float width=min(.67,_DigitCount*.22);float2 size=float2(width,.66);
     float2 q=(i.uv-(.5-size*.5))/size;
     float cell=q.x*_DigitCount;int index=clamp((int)floor(cell),0,9);
     float digit=index<4?_DigitsA[index]:index<8?_DigitsB[index-4]:_DigitsC[index-8];
     float ink=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2((digit+frac(cell))*.1,q.y)).r;
     ink*=step(0,q.x)*step(q.x,1)*step(0,q.y)*step(q.y,1);
     color=lerp(color,light,ink);
    }
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}
