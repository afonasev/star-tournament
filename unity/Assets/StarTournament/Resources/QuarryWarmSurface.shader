Shader "StarTournament/QuarryWarmSurface"
{
    Properties
    {
        [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
        [MainTexture] _BaseMap("Surface",2D)="white"{}
        _StoneMap("Shared limestone",2D)="white"{}
        _GravelMap("Shared gravel",2D)="white"{}
        _StoneScale("Stone meters",Float)=8
        _GravelScale("Gravel meters",Float)=5
        _PatchScale("Geology patch meters",Float)=9
        _BlendWidth("Geology transition",Float)=0.24
        _DustReach("Deck dust meters",Float)=5
        _Deck("Is terrain deck",Float)=0
        _DeckEdge("Authored deck front",Float)=0
        _Mode("Stone / Ground / Steel / Wood / Paint",Float)=0
        _Scale("World tile meters",Float)=6
        _Relief("Surface relief",Float)=0.08
        _Smoothness("Smoothness",Range(0,1))=0.15
        _Metallic("Metallic",Range(0,1))=0
        _Cutoff("Cutoff",Float)=0.5
        _Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_StoneMap); SAMPLER(sampler_StoneMap);
            TEXTURE2D(_GravelMap); SAMPLER(sampler_GravelMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor; float4 _BaseMap_ST;
            float _Mode, _Scale, _Relief, _Smoothness, _Metallic, _Cutoff, _Cull;
            float _StoneScale, _GravelScale, _PatchScale, _BlendWidth, _DustReach, _Deck, _DeckEdge;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; };
            struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; };
            V Vert(A a) { V o; o.w=TransformObjectToWorld(a.p.xyz); o.p=TransformWorldToHClip(o.w); o.n=TransformObjectToWorldNormal(a.n); return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p) { float2 i=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y); }
            // Surface-gradient bump consumes the already mip-filtered albedo. No extra normal texture or tangent mesh channel.
            float3 Bump(float3 p,float3 n,float h)
            {
                float3 dx=ddx(p),dy=ddy(p),r1=cross(dy,n),r2=cross(n,dx);
                float determinant=dot(dx,r1);
                float3 grad=sign(determinant)*(ddx(h)*r1+ddy(h)*r2);
                return normalize(max(abs(determinant),1e-8)*n-grad);
            }
            half4 Frag(V v):SV_Target
            {
                float3 n=normalize(v.n),an=abs(n);
                float2 uv=an.y>.6?v.w.xz:an.x>an.z?v.w.zy:v.w.xy;
                float3 color=_BaseColor.rgb; float smoothness=_Smoothness,metallic=_Metallic,height=0;
                if(_Mode<1.5)
                {
                    // The same world-space geological field crosses the original material boundaries.
                    // Shared textures replace the old offset repeat sample; no per-object material instances.
                    float2 macro=uv/_PatchScale;
                    float patch=Noise(macro+float2(Noise(macro*.47),Noise(macro*.47+13)));
                    float fine=Noise(uv*.65);
                    float soil=smoothstep(.5-_BlendWidth,.5+_BlendWidth,patch+(.5-fine)*.18);
                    float horizontal=smoothstep(.45,.8,an.y);
                    soil*=horizontal;
                    // Ground favours dust; rock favours exposed stone, with overlapping palettes.
                    soil=saturate(soil*.78+(_Mode>.5?.2:0));
                    // Dust gathers on both sides of the existing upper terrace boundary.
                    float terraceDust=1-smoothstep(0,_DustReach,abs(v.w.z-_DeckEdge));
                    soil=saturate(soil+terraceDust*horizontal*.35);
                    float2 tile=uv/_StoneScale;
                    float3 stone=SAMPLE_TEXTURE2D(_StoneMap,sampler_StoneMap,tile).rgb;
                    float3 gravel=SAMPLE_TEXTURE2D(_GravelMap,sampler_GravelMap,uv/_GravelScale).rgb;
                    color*=lerp(stone*1.12,gravel*.92,soil)*(1.08-.12*patch);
                    float3 relief=SAMPLE_TEXTURE2D_GRAD(_StoneMap,sampler_StoneMap,tile,ddx(tile)*4,ddy(tile)*4).rgb;
                    height=dot(relief,float3(.3,.59,.11))*_Relief*(1-soil*.6);
                    if(_Mode>.5 && an.y>.6)
                    {
                        // Faint tyre tread decals are shader-only; no extra geometry/collisions/renderers.
                        float2 d=v.w.xz-float2(-11,-23);
                        float2 t=float2(.883*d.x+.469*d.y,-.469*d.x+.883*d.y);
                        float track=(1-smoothstep(.32,.55,abs(abs(t.x)-1.65)))*(1-smoothstep(6,12,abs(t.y)));
                        float tread=1-smoothstep(.16,.25,abs(frac(t.y*1.7+abs(t.x)*.7)-.5));
                        color*=1-track*(.09+.09*tread);
                    }
                }
                else if(_Mode<2.5)
                {
                    float2 tile=uv/float2(2,3),edge=min(frac(tile),1-frac(tile));
                    float seam=1-smoothstep(.004,.012,min(edge.x,edge.y));
                    float weather=Noise(uv*2.1),scratch=pow(Noise(uv*float2(1.5,95)),6);
                    float rust=smoothstep(.48,.8,weather)*(1-smoothstep(.01,.075,min(edge.x,edge.y)));
                    color*=.82+.2*Hash(floor(tile))+.08*weather;
                    color=lerp(color,float3(.23,.105,.045),rust*.55);color*=1-seam*.58;
                    color+=scratch*.065;
                    float bolt=1-smoothstep(.012,.023,length(edge-float2(.045,.035)));
                    color=lerp(color,float3(.24,.26,.27),bolt);
                    height=(1-seam)*_Relief*.2; smoothness*=1-rust*.8;
                    if(_Deck>.5)
                    {
                        // A ragged, feathered deposit ties steel to limestone without altering the deck edge.
                        float drift=Noise(v.w.xz/_PatchScale),speckle=Noise(v.w.xz*1.3);
                        float edgeDistance=max(0,v.w.z-_DeckEdge);
                        float dust=(1-smoothstep(_DustReach*.12,_DustReach*(.55+drift),edgeDistance))*smoothstep(.18,.65,drift+speckle*.3);
                        dust=saturate(dust*1.4)*smoothstep(.45,.8,an.y);
                        float3 gravel=SAMPLE_TEXTURE2D(_GravelMap,sampler_GravelMap,v.w.xz/_GravelScale).rgb;
                        color=lerp(color,gravel*float3(.76,.73,.65),dust);
                        metallic*=1-dust;smoothness=lerp(smoothness,.15,dust);
                    }
                }
                else if(_Mode<3.5)
                {
                    float grain=Noise(uv*float2(2,22)),plank=frac(uv.y*2.8);
                    float seam=1-smoothstep(.012,.035,min(plank,1-plank));
                    color*=.86+.09*grain+.08*Noise(uv*.7);color*=1-.3*seam;
                    height=(grain*.12-seam*.15)*_Relief;
                }
                else
                {
                    float flecks=smoothstep(.76,.9,Noise(uv*36));
                    color=lerp(color*(.9+.1*Noise(uv*2)),float3(.22,.24,.24),flecks*.14);
                    height=Noise(uv*9)*_Relief*.1;
                }
                n=Bump(v.w,n,height);
                InputData input=(InputData)0;
                input.positionWS=v.w;input.positionCS=v.p;input.normalWS=n;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.w);
                input.shadowCoord=TransformWorldToShadowCoord(v.w);input.bakedGI=SampleSH(n);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.p);input.shadowMask=half4(1,1,1,1);
                input.vertexLighting=VertexLighting(v.w,n);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=color;surface.metallic=metallic;surface.specular=half3(.04,.04,.04);
                surface.smoothness=smoothness;surface.normalTS=half3(0,0,1);surface.occlusion=1;surface.alpha=1;
                return UniversalFragmentPBR(input,surface);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
