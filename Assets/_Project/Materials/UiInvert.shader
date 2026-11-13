Shader "Hitofuri/UiInvert"
{
    // 大量撃破のときの色反転(増永)。画面いっぱいの UI の Image に使う。
    // 出る色 = 後ろの色 × (1 - a) + (1 - 後ろの色) × a。Image の色のアルファ a で、元の色から反転した色まで混ぜる。
    // 後処理(Volume)ではなく UI で反転するのは、URP の後処理に反転が無く、WebGL でも軽く済むため
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend OneMinusDstColor OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; fixed4 color : COLOR; };
            Output Vert(Input v) { Output o; o.position = UnityObjectToClipPos(v.vertex); o.color = v.color; return o; }
            fixed4 Frag(Output i) : SV_Target { fixed a = i.color.a; return fixed4(a, a, a, a); }
            ENDCG
        }
    }
}
