Shader "Hitofuri/StageEdgeFog"
{
    Properties { _BaseColor("霧の灰色",Color)=(.16,.17,.18,.10) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Input { float4 vertex:POSITION; half4 color:COLOR; };
            struct Output { float4 position:SV_POSITION; half4 color:COLOR; };
            Output Vert(Input v){Output o;o.position=TransformObjectToHClip(v.vertex.xyz);o.color=v.color;return o;}
            half4 Frag(Output i):SV_Target{return half4(_BaseColor.rgb,_BaseColor.a*i.color.a);}
            ENDHLSL
        }
    }
}
