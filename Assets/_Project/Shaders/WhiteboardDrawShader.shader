Shader "Custom/WhiteboardDrawShader"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Overlay"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        CGINCLUDE
        #pragma target 3.0
        #include "UnityCG.cginc" // ← ① これを追加します

        sampler2D _MainTex;
        float4 _MainTex_ST;

        float4 _CircleCenter;
        float _CircleRadius;
        float _CircleOpacity;

        float4 _RectangleCenter;
        float4 _RectangleSize;
        float _RectangleAngle;

        struct appdata
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f Vert(appdata input)
        {
            v2f output;
            // ② 頂点座標をクリップ空間に正しく変換します
            output.vertex = UnityObjectToClipPos(input.vertex);
            
            // ③ Y軸の反転などに対応するため TRANSFORM_TEX マクロを使用します
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            
            return output;
        }

        float4 CopyGray(v2f input) : SV_Target
        {
            float4 source = tex2D(_MainTex, input.uv);
            float gray = dot(source.rgb, float3(0.299, 0.587, 0.114));
            return float4(gray, gray, gray, 1.0);
        }

        float4 DrawCircle(v2f input) : SV_Target
        {
            float4 source = tex2D(_MainTex, input.uv);
            float2 delta = input.uv - _CircleCenter.xy;
            float distanceFromCenter = length(delta);
            float radius = max(_CircleRadius, 1e-5);
            
            // 修正前: グラデーションになっていたため、重ね塗りで太さが変わってしまっていた
            // float falloff = saturate(1.0 - distanceFromCenter / radius);
            // float mask = falloff * falloff;

            // 修正後: 半径以内なら 1.0、外側なら 0.0 を返す step 関数を使ってくっきりした円にする
            float mask = step(distanceFromCenter, radius);

            float gray = source.r * saturate(1.0 - mask * _CircleOpacity);
            return float4(gray, gray, gray, 1.0);
        }

        float4 DrawRectangle(v2f input) : SV_Target
        {
            float4 source = tex2D(_MainTex, input.uv);
            float2 delta = input.uv - _RectangleCenter.xy;

            float angleRad = radians(_RectangleAngle);
            float sineValue = sin(-angleRad);
            float cosineValue = cos(-angleRad);
            float2 localPoint = float2(
                cosineValue * delta.x - sineValue * delta.y,
                sineValue * delta.x + cosineValue * delta.y
            );

            float2 halfSize = max(_RectangleSize.xy * 0.5, 1e-5);
            float2 normalized = abs(localPoint) / halfSize;
            float inside = step(max(normalized.x, normalized.y), 1.0);

            float gray = lerp(source.r, 1.0, inside);
            return float4(gray, gray, gray, 1.0);
        }
        ENDCG

        Pass
        {
            Name "CircleDarken"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment DrawCircle
            ENDCG
        }

        Pass
        {
            Name "RectangleErase"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment DrawRectangle
            ENDCG
        }

        Pass
        {
            Name "CopyGray"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment CopyGray
            ENDCG
        }
    }

    FallBack Off
}