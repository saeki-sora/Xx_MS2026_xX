// 歯のコアの本体の画素の処理（CoreTooth.shader から、ObjectToonPasses.hlsl の後に読む）。
// トゥーンの光（ObjectToonShade）・仕上げ（ObjectToonGrade）はステージの物と同じ関数を使い、
// その前に「汚れ」で色を変え、その後に「ふちの光」「ひび」「ダメージの白い光」を足す。
// 値は CoreToothLook が MaterialPropertyBlock で入れる（材質の値ではないので、ここで宣言する）。
#ifndef MS2026_CORE_TOOTH_INCLUDED
#define MS2026_CORE_TOOTH_INCLUDED

float4 _ToothState;      // x=HP(0〜1) y=傷み具合 z=ひびの広がり w=ダメージの白い光
float4 _ToothLook;       // x=汚れ(0〜1) y=ふちの光の強さ z=ふちの光の幅 w=ふちの光のぼかし
float4 _ToothNoise;      // x=しみの細かさ y=根元から汚れる量 z=ひびの細かさ w=ひびの線の太さ
float4 _ToothPulse;      // x=HP が多いときの脈の速さ y=HP が少ないときの脈の速さ（回/秒）
float4 _ToothFrame;      // xyz=モデルの真ん中（モデルの座標） w=1/高さ（模様の大きさをモデルの大きさにそろえる）
float4 _ToothUp;         // xyz=「上」（カメラ側）の向き（モデルの座標）
half4  _ToothGlowColor;  // ふちの光（HP が多いとき）
half4  _ToothHurtColor;  // ふちの光（HP が少ないとき）
half4  _ToothCrackColor; // ひびから漏れる光
half4  _ToothInkColor;   // ひびの線
half4  _ToothStainColor; // しみ（黄ばみ）
half4  _ToothStainDeepColor; // しみ（HP が少ないときの茶色）

float ToothHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float3 ToothHash33(float3 p)
{
    return frac(sin(float3(dot(p, float3(127.1, 311.7, 74.7)), dot(p, float3(269.5, 183.3, 246.1)), dot(p, float3(113.5, 271.9, 124.6)))) * 43758.5453);
}

float ToothNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(ToothHash(i), ToothHash(i + float3(1, 0, 0)), f.x), lerp(ToothHash(i + float3(0, 1, 0)), ToothHash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(ToothHash(i + float3(0, 0, 1)), ToothHash(i + float3(1, 0, 1)), f.x), lerp(ToothHash(i + float3(0, 1, 1)), ToothHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

float ToothFbm(float3 p)
{
    return ToothNoise(p) * 0.55 + ToothNoise(p * 2.03 + 1.7) * 0.3 + ToothNoise(p * 4.1 + 3.1) * 0.15;
}

// ボロノイ（ひび割れ）: x=ひびの線までの距離, y=その線の場所ごとの乱数。
float2 ToothCrackEdge(float3 p)
{
    float3 cell = floor(p);
    float3 f = frac(p);
    float d1 = 8.0;
    float d2 = 8.0;
    float3 nearest = 0;
    [unroll] for (int z = -1; z <= 1; z++)
    {
        [unroll] for (int y = -1; y <= 1; y++)
        {
            [unroll] for (int x = -1; x <= 1; x++)
            {
                float3 offset = float3(x, y, z);
                float3 site = offset + ToothHash33(cell + offset) - f;
                float d = dot(site, site);
                if (d < d1)
                {
                    d2 = d1;
                    d1 = d;
                    nearest = cell + offset;
                }
                else if (d < d2)
                {
                    d2 = d;
                }
            }
        }
    }

    return float2(sqrt(d2) - sqrt(d1), ToothHash(nearest + 7.7));
}

half4 CoreToothForwardFragment(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float3 positionWS = input.positionWSAndFog.xyz;
    half4 baseColor = ObjectToonBaseColor(input.uv);

    // 模様はモデルの座標で付ける（コアが落ちて動いても、模様は歯に貼り付いたまま）。
    float3 fromCenter = TransformWorldToObject(positionWS) - _ToothFrame.xyz;
    float3 p = fromCenter * _ToothFrame.w;
    float height01 = saturate(dot(fromCenter, _ToothUp.xyz) * _ToothFrame.w + 0.5);

    float damage = saturate(_ToothState.y);
    float crackAmount = saturate(_ToothState.z);
    float hitFlash = saturate(_ToothState.w);
    float dirt = saturate(_ToothLook.x);

    // ── 汚れ: 黄ばみのしみが根元（下）から広がる ──
    float spot = ToothFbm(p * _ToothNoise.x + 2.0) * (1.0 - _ToothNoise.y) + height01 * _ToothNoise.y;
    half stain = smoothstep(spot - 0.025, spot + 0.025, dirt * 1.05);
    half3 stainColor = lerp(_ToothStainColor.rgb, _ToothStainDeepColor.rgb, smoothstep(0.55, 1.0, dirt));

    ObjectToonSurface s;
    s.albedo = lerp(baseColor.rgb, stainColor, stain);
    s.emission = _EmissionColor.rgb;
    s.normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(isFrontFace, 1.0h, -1.0h);
    s.viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
    s.positionWS = positionWS;
    s.positionCS = input.positionCS;
    s.uv = input.uv;

    half3 color = ObjectToonShade(s);

    // ── ふちの光: 帯がゆっくり脈打つ。HP が減るほど赤く速く ──
    float rate = lerp(_ToothPulse.x, _ToothPulse.y, damage);
    float pulse = 0.5 + 0.5 * sin(_Time.y * rate * TWO_PI);
    half3 glowColor = lerp(_ToothGlowColor.rgb, _ToothHurtColor.rgb, damage);
    half edge = 1.0h - saturate(dot(s.normalWS, s.viewDirWS));
    half rimStart = 1.0h - _ToothLook.z;
    half rimSoft = max(_ToothLook.w * 0.5h, 1e-3h);
    half rim = smoothstep(rimStart - rimSoft, rimStart + rimSoft, edge);
    color = lerp(color, glowColor, saturate(rim * (0.35 + 0.45 * pulse) * _ToothLook.y));

    // ── ひび: 描いた暗い線が少しずつ入り、線の中から赤い光が漏れる ──
    half ink = 0;
    half crackCore = 0;
    if (crackAmount > 0.001)
    {
        float2 crack = ToothCrackEdge(p * _ToothNoise.z + 11.3);
        float width = _ToothNoise.w * (0.6 + 0.6 * crackAmount);
        half reveal = step(crack.y * 0.6 + ToothNoise(p * 4.0) * 0.4, crackAmount * 1.1);
        ink = (1.0 - smoothstep(width * 0.45, width * 0.55, crack.x)) * reveal;
        crackCore = (1.0 - smoothstep(width * 0.12, width * 0.22, crack.x)) * reveal;
        half3 crackLight = _ToothCrackColor.rgb * (1.1 + 1.4 * pulse * damage);
        color = lerp(color, _ToothInkColor.rgb, ink);
        color = lerp(color, crackLight, crackCore);
    }

    // ── ダメージを受けた瞬間の光 ──
    // 歯の面だけを白く光らせ、ひびの暗い線は暗いまま残す（線まで白くすると、敵が続けて着く間ずっとひびが消えて見える）。
    // ひびの中の光は、白ではなくひびの色のまま強くする。
    color += hitFlash * 0.35 * (1.0 - ink);
    color += hitFlash * crackCore * _ToothCrackColor.rgb * 1.2;

    color = ObjectToonGrade(color);
    color = MixFog(color, input.positionWSAndFog.w);
    return half4(color, 1.0h);
}

#endif
