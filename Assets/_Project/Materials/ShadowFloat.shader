Shader "Hitofuri/ShadowFloat"
{
    Properties
    {
        [MainColor] _BaseColor("墨色", Color)=(.014,.016,.019,1)
        [HDR] _EmissionColor("白い発光", Color)=(0,0,0,1)
        _GlowRatio("白く光る割合", Range(0,1))=0
        _FloatAmplitude("浮遊の揺れ幅", Float)=.045
        _FloatSpeed("浮遊の周期", Float)=1.8
        _FloatPhase("浮遊の位相", Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _EmissionColor;
            float _GlowRatio, _FloatAmplitude, _FloatSpeed, _FloatPhase;
            CBUFFER_END
            struct Input { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Output { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half fog:TEXCOORD2; };
            Output Vert(Input v)
            {
                Output o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionWS.y+=sin(_Time.y*_FloatSpeed+_FloatPhase)*_FloatAmplitude;
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Output i):SV_Target
            {
                half3 n=normalize(i.normalWS);
                half3 illumination=SampleSH(n);
                Light main=GetMainLight();
                illumination+=main.color*saturate(dot(n,main.direction))*main.distanceAttenuation;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                uint count=GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                {
                    Light l=GetAdditionalLight(lightIndex,i.positionWS);
                    illumination+=l.color*saturate(dot(n,l.direction))*l.distanceAttenuation;
                }
                #endif
                LIGHT_LOOP_BEGIN(count)
                    Light l=GetAdditionalLight(lightIndex,i.positionWS);
                    illumination+=l.color*saturate(dot(n,l.direction))*l.distanceAttenuation;
                LIGHT_LOOP_END
                #endif
                half rim=pow(1-saturate(dot(n,GetWorldSpaceNormalizeViewDir(i.positionWS))),3);
                // 刀の光に色があっても、敵と舞台は白黒のままに保つ。
                half3 weights=half3(.2126,.7152,.0722);
                half intensity=dot(illumination,weights),baseValue=dot(_BaseColor.rgb,weights);
                // 蓄積は輪郭と明暗差で示し、全身を白一色にして移動姿勢を潰さない。
                half glow=saturate(_GlowRatio);
                half value=lerp(baseValue,.12h,glow)*(intensity+.12h)+rim*(.10h+saturate(glow)*.60h)+dot(_EmissionColor.rgb,weights)+glow*.035h;
                half3 color=half3(value,value,value);
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
