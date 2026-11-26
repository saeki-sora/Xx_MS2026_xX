Shader "MS2026/UI/ClearLogoNoise"
{
    Properties
    {
        [PerRendererData] _MainTex ("CLEAR image", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Noise dissolve", Range(0,1)) = 0
        _NoiseTime ("Noise time", Float) = 0
        _StencilComp ("Stencil comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil operation", Float) = 0
        _StencilWriteMask ("Stencil write mask", Float) = 255
        _StencilReadMask ("Stencil read mask", Float) = 255
        _ColorMask ("Color mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Progress, _NoiseTime;
            float4 _ClipRect;

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float progress = saturate(_Progress);
                float enabled = step(0.0001, progress);
                float band = floor(i.uv.y * 100);
                float tick = floor(_NoiseTime * 36);
                float jitter = (Hash(float2(band, tick)) - 0.5) * 0.045 * progress * enabled;
                float2 uv = i.uv + float2(jitter, 0);
                fixed4 c = tex2D(_MainTex, uv);
                float split = 0.004 * progress * enabled;
                c.r = tex2D(_MainTex, uv + float2(split,0)).r;
                c.b = tex2D(_MainTex, uv - float2(split,0)).b;
                // 消えるブロックは固定の乱数で決め、消滅後のちらつく復活を防ぐ。
                float dissolve = Hash(floor(i.uv * float2(140,80)));
                c.a *= lerp(1, step(progress, dissolve), enabled);
                c.a *= 1 - smoothstep(0.75,1,progress);
                float scan = step(0.93, Hash(float2(band,tick + 53))) * progress * enabled;
                c.rgb = lerp(c.rgb, float3(0.8,1,1), scan);
                c *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
