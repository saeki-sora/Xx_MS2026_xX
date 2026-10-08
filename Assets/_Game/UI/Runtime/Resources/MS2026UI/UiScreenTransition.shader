// 画面切り替えの幕（全画面）。_Progress が 0 で何も覆っていない、1 で完全に覆う。
// 模様ごとに「その画素が覆われる順番（0〜1）」を作り、_Progress がそれを超えた画素から塗る。
// _Pattern: 0=フェード 1=ワイプ 2=まるく閉じる 3=ブラインド 4=ひし形 5=ルール画像 6=灼ける（ふちが光りながら焼ける）
Shader "MS2026/UI/ScreenTransition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("幕の色", Color) = (0,0,0,1)
        _Progress ("進み具合", Range(0,1)) = 0
        _Pattern ("模様", Float) = 0
        _Angle ("向き(度)", Float) = 0
        _Count ("数", Float) = 8
        _Softness ("境目のぼかし", Range(0.001,0.5)) = 0.05
        _Center ("中心(0-1)", Vector) = (0.5,0.5,0,0)
        _RuleTex ("ルール画像", 2D) = "gray" {}
        _Invert ("逆にする", Float) = 0
        [HDR] _EdgeColor ("ふちの色", Color) = (4,1.4,0.3,1)
        _EdgeWidth ("ふちの太さ", Range(0,0.3)) = 0.06
        _Aspect ("画面の横長さ", Float) = 1.7777
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "False" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            fixed4 _Color;
            float _Progress, _Pattern, _Angle, _Count, _Softness, _Invert, _EdgeWidth, _Aspect;
            float4 _Center;
            fixed4 _EdgeColor;
            sampler2D _RuleTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            float noise2(float2 p)
            {
                float2 i = floor(p); float2 f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), f.x), f.y);
            }

            // その画素が覆われる順番（0=最初に覆われる、1=最後）。
            float order(float2 uv)
            {
                float2 c = uv - 0.5;
                c.x *= _Aspect;
                float a = radians(_Angle);
                float2 dir = float2(cos(a), sin(a));
                int pattern = (int)round(_Pattern);

                if (pattern == 1) // ワイプ
                {
                    float extent = abs(dir.x) * _Aspect * 0.5 + abs(dir.y) * 0.5;
                    return saturate(dot(c, dir) / (2 * extent) + 0.5);
                }
                if (pattern == 2) // まるく閉じる（外側から中心へ）
                {
                    float2 p = uv - _Center.xy; p.x *= _Aspect;
                    float maxD = length(float2(max(_Center.x, 1 - _Center.x) * _Aspect, max(_Center.y, 1 - _Center.y)));
                    return 1 - saturate(length(p) / maxD);
                }
                if (pattern == 3) // ブラインド
                {
                    float t = dot(c, dir) * _Count / _Aspect;
                    return frac(t);
                }
                if (pattern == 4) // ひし形
                {
                    float2 g = uv * float2(_Count * _Aspect, _Count);
                    float2 cell = frac(g) - 0.5;
                    float d = abs(cell.x) + abs(cell.y);
                    float sweep = saturate(dot(floor(g) / float2(_Count * _Aspect, _Count) - 0.5, dir) + 0.5);
                    return saturate(d * 0.5 + sweep * 0.5);
                }
                if (pattern == 5) // ルール画像（白いほど後から覆う）
                {
                    return tex2D(_RuleTex, uv).r;
                }
                if (pattern == 6) // 灼ける
                {
                    float n = noise2(uv * float2(6 * _Aspect, 6)) * 0.65 + noise2(uv * float2(18 * _Aspect, 18)) * 0.35;
                    float2 p = uv - _Center.xy; p.x *= _Aspect;
                    return saturate(n * 0.75 + length(p) * 0.35);
                }
                return 0.5; // フェード（全体が一緒に）
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float o = order(i.uv);
                if (_Invert > 0.5) o = 1 - o;

                int pattern = (int)round(_Pattern);
                if (pattern == 0)
                {
                    return fixed4(_Color.rgb, _Color.a * _Progress * i.color.a);
                }

                // 境目のぼかしの分だけ進み具合を広げ、0 で何も無く 1 で全部覆うようにする。
                float p = _Progress * (1 + _Softness);
                float cover = smoothstep(o, o + _Softness, p);
                fixed4 col = fixed4(_Color.rgb, _Color.a * cover * i.color.a);

                if (pattern == 6 && _EdgeWidth > 0)
                {
                    float edge = smoothstep(o - _EdgeWidth, o, p) * (1 - cover);
                    col.rgb = lerp(col.rgb, _EdgeColor.rgb, saturate(edge));
                    col.a = max(col.a, edge * i.color.a);
                }

                return col;
            }
            ENDCG
        }
    }
}
