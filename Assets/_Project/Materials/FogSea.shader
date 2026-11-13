// 島の下に敷く霧の海。板を深さ違いで重ね、落ちた敵が層を抜けるたびに薄れて見えなくなるようにする
// テクスチャを持たずに手続きノイズで濃淡を作るのは、ダウンロード量を増やさず板の大きさに縛られないようにするため
Shader "Hitofuri/FogSea"
{
    Properties
    {
        _BaseColor("霧の色", Color) = (0.2, 0.21, 0.22, 1)
        _Density("濃さ", Range(0, 1)) = 0.5
        _Contrast("濃淡の差", Range(0, 1)) = 0.6
        _NoiseScale("ノイズの大きさ(m)", Float) = 18
        _FlowA("流れA(xz、m/秒)", Vector) = (0.4, 0.15, 0, 0)
        _FlowB("流れB(xz、m/秒)", Vector) = (-0.25, 0.3, 0, 0)
        _FadeCenter("端を消す中心(xz)", Vector) = (0, 0, 0, 0)
        _FadeRadius("端を消す半径(m)", Float) = 140
        _FadeWidth("端を消す幅(m)", Float) = 40
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
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
            half _Density;
            half _Contrast;
            float _NoiseScale;
            float4 _FlowA;
            float4 _FlowB;
            float4 _FadeCenter;
            float _FadeRadius;
            float _FadeWidth;
            CBUFFER_END

            struct Input { float4 vertex : POSITION; };
            struct Output { float4 position : SV_POSITION; float3 worldPos : TEXCOORD0; };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // WebGL の負荷を抑えるため2オクターブで止める
            float Fbm(float2 p)
            {
                return ValueNoise(p) * 0.65 + ValueNoise(p * 2.03 + 7.7) * 0.35;
            }

            Output Vert(Input v)
            {
                Output o;
                o.worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.position = TransformWorldToHClip(o.worldPos);
                return o;
            }

            half4 Frag(Output i) : SV_Target
            {
                // ワールド座標でノイズを取るので、板の拡大率を変えても霧の模様の大きさは変わらない
                float scale = max(_NoiseScale, 0.01);
                float2 p = i.worldPos.xz / scale;
                float t = _Time.y / scale;
                float n1 = Fbm(p + _FlowA.xy * t);
                float n2 = Fbm(p * 1.7 + _FlowB.xy * t + 13.1);
                // 2つを平均すると値が0.5付近に寄って濃淡が消えるので、smoothstep で広げて霧の塊と切れ間を作る
                float n = smoothstep(0.3, 0.7, (n1 + n2) * 0.5);

                half shade = lerp(1.0, n * 2.0, _Contrast);
                half alpha = saturate(_Density * shade);

                // 板の四角い端が見えないよう、中心から離れるほど透明にして背景の闇へ溶かす
                float dist = distance(i.worldPos.xz, _FadeCenter.xy);
                alpha *= 1.0 - smoothstep(_FadeRadius - _FadeWidth, _FadeRadius, dist);

                half3 color = _BaseColor.rgb * lerp(0.8, 1.2, n);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
