// 画面のふちの黒い靄（EdgeMistLook が描くカメラごとに入れるグローバル値）。
// ・画面の後処理: MS2026/Fortress/EdgeMist（靄そのもの・夜の見え方・ビームの光だまり）。
// ・群衆の敵: SwarmSprite（靄の中だけ、冷たい縁の光を付ける）。
// 靄の濃さは「コアの画面上の位置から、その画素を通って画面の端まで伸ばした線」のうち、どこまで来ているか（0=コア, 1=画面の端）で決める。
// コアが画面の真ん中なら外周がぐるりと、コアが画面の下寄りなら上の端（コアと反対側）に厚く靄が出る。
// 値が一度も入っていないとき（靄の無いシーン・靄を描かないカメラ）は w=0 なので、何も変わらない。
#ifndef MS2026_FORTRESS_EDGE_MIST_INCLUDED
#define MS2026_FORTRESS_EDGE_MIST_INCLUDED

float4 _FortressEdgeMistCore;  // xy=コアの画面上の位置(0〜1), z=画面の横÷縦, w=1なら靄を描く
float4 _FortressEdgeMistShape; // x=始まる位置, y=濃くなるまでの幅, z=コアに近い側の端を弱める量, w=ふちのゆらぎ
float4 _FortressEdgeMistFlow;  // x=ゆらぎの細かさ, y=ゆらぎの流れる速さ, z=靄の形の四角さ(小さいほど角が丸い)
float4 _FortressEdgeMistRimColor; // 靄の中の敵の縁の光の色(HDR)
float _FortressEdgeMistRimWidth;  // 縁の光の太さ（敵の絵の大きさに対する割合。0で出さない）

float FortressEdgeMistHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float FortressEdgeMistValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = FortressEdgeMistHash(i);
    float b = FortressEdgeMistHash(i + float2(1, 0));
    float c = FortressEdgeMistHash(i + float2(0, 1));
    float d = FortressEdgeMistHash(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 0〜1 のもやもやした模様（3段重ね）。
float FortressEdgeMistFbm(float2 p)
{
    float v = FortressEdgeMistValueNoise(p) * 0.55;
    v += FortressEdgeMistValueNoise(p * 2.03 + 17.1) * 0.3;
    v += FortressEdgeMistValueNoise(p * 4.11 - 31.7) * 0.15;
    return v;
}

// 画素（画面の0〜1の位置）の靄の濃さ 0〜1。
half FortressEdgeMistAmount(float2 uv)
{
    if (_FortressEdgeMistCore.w < 0.5)
    {
        return 0.0;
    }

    float2 core = _FortressEdgeMistCore.xy;
    float aspect = _FortressEdgeMistCore.z;
    float2 d = uv - core;
    // 横・縦それぞれ「コアからその向きの画面の端まで」のどこまで来ているか(0〜1)を、角を丸めてまとめる。
    // 大きい方だけを使うと、コアから画面の角へ向かう線で靄の形が折れ曲がるので、累乗の和で丸める（大きいほど四角に近い）。
    float2 room = lerp(core, 1.0 - core, step(0.0, d));
    float2 q = abs(d) / max(room, 1e-4);
    float roundness = max(_FortressEdgeMistFlow.z, 1.0);
    float ratio = pow(pow(q.x, roundness) + pow(q.y, roundness), 1.0 / roundness); // 0=コア, 1=画面の端あたり
    float rayLength = length(d * float2(aspect, 1.0)) / max(ratio, 1e-4);          // コア〜端の長さ（画面の高さ=1）
    // コアのすぐ近くの端（線が短い所）は靄を弱める。
    float nearSide = lerp(1.0, saturate((rayLength - 0.2) / 0.6), _FortressEdgeMistShape.z);

    float2 p = uv * float2(aspect, 1.0) * _FortressEdgeMistFlow.x;
    float t = _Time.y * _FortressEdgeMistFlow.y;
    float n = FortressEdgeMistFbm(p + float2(t, -t * 0.7)) * 0.65 + FortressEdgeMistFbm(p * 1.7 - float2(t * 0.6, t)) * 0.35;
    float r = ratio + (n - 0.5) * _FortressEdgeMistShape.w * 2.0;
    return (half)(smoothstep(_FortressEdgeMistShape.x, _FortressEdgeMistShape.x + _FortressEdgeMistShape.y, r) * nearSide);
}

#endif
