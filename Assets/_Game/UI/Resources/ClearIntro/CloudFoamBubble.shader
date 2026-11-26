Shader "MS2026/UI/CloudFoamBubble"
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

            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 film:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; float2 film:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
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
                o.film = v.film;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float distance = length(p);
                float aa = max(fwidth(distance), 0.001);
                float coverage = 1 - smoothstep(1 - aa, 1, distance);
                // Opaque white foam with soft film edges and restrained reflections.
                float dome = sqrt(saturate(1 - dot(p, p)));
                float shade = (1 - smoothstep(-0.95, 0.5, p.y)) * 0.42;
                float3 tint = i.color.rgb;
                float3 pearl = lerp(tint, float3(1,1,1), dome * 0.26);
                pearl = lerp(pearl, tint * 0.65, shade);
                // Use a pixel-aware stroke so small bubbles remain legible without a heavy border.
                float rimWidth = clamp(aa * 1.8, 0.018, 0.065);
                float rim = smoothstep(1 - rimWidth - aa, 1 - rimWidth, distance);
                pearl = lerp(pearl, tint * 0.53, rim * 0.78);
                float2 light = (p - float2(-0.25,0.32)) * float2(1.8,2.4);
                pearl = lerp(pearl, float3(1,1,1), exp(-dot(light,light)) * 0.9 * (1 - rim));
                float2 glint = (p - float2(-0.35,0.40)) * float2(6,10);
                pearl = lerp(pearl, float3(1,1,1), exp(-dot(glint,glint)) * 0.95);
                // A restrained curved reflection gives the thin film depth without rainbow tint.
                float arc = exp(-pow((distance - 0.86) * 34, 2));
                float upperLight = smoothstep(0.1, 0.6, p.y) * (1 - smoothstep(-0.35, 0.6, p.x));
                pearl = lerp(pearl, float3(1,1,1), arc * upperLight * 0.85);
                float lowerArc = exp(-pow((distance - 0.78) * 25, 2)) * (1 - smoothstep(-0.6,-0.1,p.y));
                pearl = lerp(pearl, float3(0.85,0.95,0.98), lowerArc * 0.22);
                // The film tears from an off-centre point; the hole expands into the outer rim.
                float phase = i.film.y * 6.2831853;
                float2 tearOrigin = float2(cos(phase), sin(phase)) * 0.28;
                float2 tearVector = p - tearOrigin;
                float tearDistance = length(tearVector);
                float tear = smoothstep(0.18, 0.78, i.film.x);
                float tearRadius = tear * 1.6;
                float tearAA = max(fwidth(tearDistance), 0.002);
                float membrane = smoothstep(tearRadius - tearAA, tearRadius + tearAA, tearDistance);
                float lip = exp(-pow((tearDistance - tearRadius) / max(tearAA * 2, 0.015), 2)) * step(0.001, tear);
                pearl = lerp(pearl, float3(1,1,1), lip * 0.9);
                coverage *= lerp(1, membrane, step(0.001, tear));
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


