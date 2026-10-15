Shader "StarTournament/WellContactSurface"
{
    Properties
    {
        [MainColor] _BaseColor("Colour",Color)=(1,1,1,1)
        [MainTexture] _BaseMap("Albedo",2D)="white"{}
        _BumpMap("Normal",2D)="bump"{}
        _BumpScale("Relief",Float)=1
        _Smoothness("Smoothness",Range(0,1))=.3
        _Metallic("Metallic",Range(0,1))=0
        _OcclusionStrength("Contact shade",Range(0,1))=1
        _Cull("Cull",Float)=2
        _Cutoff("Cutoff",Float)=.5
        _Surface("Surface",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "WellForward" Tags {"LightMode"="UniversalForwardOnly"}
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half visibility:TEXCOORD4;half fog:TEXCOORD5;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A input)
            {
                V o=(V)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(input.normalOS,input.tangentOS);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=n.normalWS;o.tangentWS=half4(n.tangentWS,input.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(input.uv,_BaseMap);o.visibility=input.color.r;o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            half4 Frag(V input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;InitializeStandardLitSurfaceData(input.uv,surface);
                InputData data=(InputData)0;data.positionWS=input.positionWS;
                half3 bitangent=cross(input.normalWS,input.tangentWS.xyz)*input.tangentWS.w;
                data.normalWS=NormalizeNormalPerPixel(TransformTangentToWorld(surface.normalTS,half3x3(input.tangentWS.xyz,bitangent,input.normalWS)));
                data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI=SampleSH(data.normalWS);data.vertexLighting=VertexLighting(input.positionWS,data.normalWS);data.shadowMask=half4(1,1,1,1);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                // Physical local visibility only modulates indirect light, preserving direct lamp/specular response.
                surface.occlusion*=lerp(1,input.visibility,_OcclusionStrength);
                half4 result=UniversalFragmentPBR(data,surface);result.rgb=MixFog(result.rgb,input.fog);return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
