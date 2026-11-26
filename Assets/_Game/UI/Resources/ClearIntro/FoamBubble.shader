Shader "MS2026/UI/FoamBubble"
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
            #pragma target 3.0
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
                float2 p = i.uv * 2 - 1;
                float distance = length(p);
                float aa = max(fwidth(distance), 0.001);
                float coverage = 1 - smoothstep(1 - aa, 1, distance);
                float dome = sqrt(saturate(1 - dot(p, p)));
                float phase = i.color.r * 51 + i.color.b * 27;
                float drift = sin(_Time.y * 0.65 + phase) * 0.025;
                float lowerShade = (1 - smoothstep(-0.95, 0.15, p.y)) * 0.12;
                float3 pearl = lerp(i.color.rgb, float3(1, 1, 1), dome * 0.35);
                pearl = lerp(pearl, float3(0.66, 0.88, 0.82), lowerShade);
                float rim = smoothstep(0.84, 0.985, distance);
                pearl = lerp(pearl, float3(0.64, 0.85, 0.80), rim * 0.48);
                float2 highlight = p - float2(-0.30 + drift, 0.40);
                float softLight = exp(-dot(highlight * float2(2.9, 4.6), highlight * float2(2.9, 4.6)));
                pearl = lerp(pearl, float3(1,1,1), softLight * 0.95);
                float crescent = exp(-pow((distance - 0.91) * 30, 2)) * smoothstep(0.05, 0.65, p.y) * (1 - smoothstep(-0.55, 0.65, p.x));
                pearl = lerp(pearl, float3(1,1,1), crescent * 0.9);
                float2 glint = (p - float2(0.38, -0.30)) * 23;
                pearl = lerp(pearl, float3(1,1,1), exp(-dot(glint,glint)) * 0.7);
                fixed4 c = fixed4(pearl, coverage * i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}

