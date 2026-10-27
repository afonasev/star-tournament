Shader "StarTournament/ForgeReactor"
{
 Properties
 {
  _FlowMap("Authored reactor flow",2D)="black"{}
  _Flux("Volumetric filament mesh",Float)=0
  _ReactorClock("Session time, shot, speed, breathing",Vector)=(0,0,.85,.12)
  _ReactorMotion("Intensity, shot gain",Vector)=(1.65,.45,0,0)
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_FlowMap); SAMPLER(sampler_FlowMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _ReactorClock,_ReactorMotion;
   float4x4 _ObjectToReactor,_ReactorToObject;
   float _Flux;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;float3 reactor:TEXCOORD2;};
   float2 rotateFlow(float2 p,float a){float s,c;sincos(a,s,c);return float2(c*p.x-s*p.y,s*p.x+c*p.y);}
   Varyings vert(Attributes a)
   {
    Varyings o;float3 r=mul(_ObjectToReactor,a.positionOS).xyz;
    float t=_ReactorClock.x*_ReactorClock.z;
    if(_Flux>.5)
    {
     float radius=length(r.xy)/.099;
     // Differential advection bends the existing tubes, never a rigid propeller rotation.
     float theta=atan2(r.y,r.x);
     // Bounded shear: its radial derivative cannot grow with match duration.
     float angle=t*.65+.7*sin(radius*8-t*.7+theta)+.3*sin(radius*17+t*.41+theta*2);
     r.xy=rotateFlow(r.xy,angle)*(1-.055*_ReactorClock.y*(1-radius));
     r.z+=.0025*sin(radius*16-t*1.1);
     a.positionOS=mul(_ReactorToObject,float4(r,1));
    }
    o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
    o.uv=a.uv;o.reactor=r;return o;
   }
   // Relative spectral weights define the authored flow shape. Lab owns the overall speed, breathing and brightness.
   float flowLayer(float2 p,float t,float layer)
   {
    float radius=length(p);float theta=atan2(p.y,p.x);
    float angle=t*(.38+layer*.19)+.62*sin(radius*5-t*.37+layer*2)+.18*sin(radius*16-t*.57+layer*2);
    float2 q=rotateFlow(p,angle);
    q*=1+.045*sin(theta*3+radius*12-t*.81+layer);
    float authored=SAMPLE_TEXTURE2D(_FlowMap,sampler_FlowMap,q*.5+.5).g;
    float bend=.52*sin(theta*3-radius*9+t*.44+layer*2)+.19*sin(theta*7+radius*17-t*.67);
    float phase=theta*(3+layer)+radius*(10+layer*3)-t*(1.5+layer*.29)+bend;
    float strand=pow(saturate(1-abs(sin(phase))*6),3);
    float breaks=.4+.6*(.5+.5*sin(radius*23+theta*2+t*.92+layer));
    float halo=pow(saturate(1-abs(sin(phase))*2.2),2);
    return authored*.5+(strand*.9+halo*.14)*breaks;
   }
   half4 frag(Varyings i):SV_Target
   {
    float time=_ReactorClock.x,t=time*_ReactorClock.z,shot=_ReactorClock.y;
    float breathing=1+_ReactorClock.w*(.56*sin(time*1.37)+.28*sin(time*2.19+.8)+.16*sin(time*.73+2));
    if(_Flux>.5)
    {
     float r=length(i.reactor.xy)/.099;
     float theta=atan2(i.reactor.y,i.reactor.x);
     float packet=pow(.5+.5*sin(r*27-t*3.1+theta*2),3);
     float hot=(.18+packet*1.2)*breathing*(1+shot*_ReactorMotion.y);
     return half4(float3(.25,1,.045)*hot*_ReactorMotion.x,1);
    }
    float2 p=i.uv*2-1;float r=length(p);
    float3 viewOS=mul((float3x3)_ObjectToReactor,TransformWorldToObjectDir(GetWorldSpaceViewDir(i.positionWS),true));
    float2 parallax=viewOS.xy/max(abs(viewOS.z),.4)*.13;
    float a=flowLayer(p,t,0),b=flowLayer(p+parallax*.65,t*.82,1),c=flowLayer(p+parallax,t*.61,2);
    float envelope=saturate((1-r)*4);envelope*=envelope*(3-2*envelope);
    float energy=(a*.62+b*.3+c*.2)*envelope;
    float cloud=(.5+.5*sin(atan2(p.y,p.x)*3+r*12-t*.7))*(.5+.5*cos(r*18+t*.51));
    float core=exp(-r*r*95)*(.22+.08*sin(t*2));
    float3 color=float3(.002,.013,.006)+float3(.004,.09,.028)*cloud*envelope;
    color+=(float3(.23,1,.035)*energy+float3(.38,.8,.09)*core)*breathing;
    color*= _ReactorMotion.x*(1+shot*_ReactorMotion.y);
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}
