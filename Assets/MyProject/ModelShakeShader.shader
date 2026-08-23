Shader "Custom/ModelShakeShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        // C#（MaterialPropertyBlock）から操作するプロパティ
        _ShakeAmount ("Shake Amount", Float) = 0.0
        _ShakeSpeed ("Shake Speed", Float) = 100.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // ★修正点：vert:vert ではなく vertex:vert が正しい命令です
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        float _ShakeAmount;
        float _ShakeSpeed;

        // 頂点シェーダー（ノイズシェイク）
        void vert(inout appdata_full v) {
            if (_ShakeAmount > 0.0) {
                // 頂点のローカル座標をベースに、時間（_Time.y）で激しく変化する波を作ります
                float shakeX = sin(_Time.y * _ShakeSpeed + v.vertex.y * 100.0);
                float shakeY = cos(_Time.y * _ShakeSpeed + v.vertex.x * 100.0);
                
                // 計算したノイズ揺れに強さを掛けて頂点に加算
                v.vertex.x += shakeX * _ShakeAmount;
                v.vertex.y += shakeY * _ShakeAmount;
            }
        }

        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
