// 輪郭線の太さ（カメラの距離・画角で見た目の太さをそろえる）と、輪郭線を奥へずらす ZOffset。
// 元: NiloOutlineUtil.hlsl / NiloZOffset.hlsl（ColinLeung-NiloCat, MIT。ThirdPartyNotices を参照）。
// 変更点: 画角を atan で求めず、射影行列の値（1/tan(画角/2)）をそのまま使う（頂点ごとの計算を軽く）。
//         押し出す向きを「法線」と「物の中心から外向き」の間で選べるようにした（角ばった物の輪郭の割れ対策）。
#ifndef MS2026_OBJECT_TOON_OUTLINE_UTIL_INCLUDED
#define MS2026_OBJECT_TOON_OUTLINE_UTIL_INCLUDED

// 画面上でだいたい同じ太さになる、ワールドでの押し出し量（_OutlineWidth = 1 のとき）。
float ObjectToonOutlineScale(float positionVSZ)
{
    if (unity_OrthoParams.w == 0)
    {
        // 透視: 距離 × tan(画角/2) に比例させ、どの距離でも画面上の太さをそろえる
        // （元は距離を1mで頭打ちにしてキャラ向けにしていた。背景の物はカメラから遠いので頭打ちをやめた）。
        // 係数は、幅1のとき画面の高さ1080pxでおよそ3pxになる値。
        float tanHalfFov = 1.0 / max(abs(UNITY_MATRIX_P[1][1]), 1e-4);
        return abs(positionVSZ) * tanHalfFov * 0.0052;
    }

    // 正射影: 元と同じ（orthoSize × 50 × 0.00005）。
    return abs(unity_OrthoParams.y) * 0.0025;
}

float3 ObjectToonOutlinePositionWS(float3 positionWS, half3 normalWS)
{
    float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
    float3 fromCenter = positionWS - centerWS;
    float3 fromCenterDir = dot(fromCenter, fromCenter) > 1e-8 ? normalize(fromCenter) : (float3)normalWS;
    float3 direction = normalize(lerp((float3)normalWS, fromCenterDir, saturate(_OutlineDirection)) + 1e-5);

    float positionVSZ = TransformWorldToView(positionWS).z;
    float amount = _OutlineWidth * ObjectToonOutlineScale(positionVSZ);
    return positionWS + direction * amount;
}

// 頂点をカメラから見て奥へ押したときの深度だけを書き換える（形は変えない）。物の表面に輪郭線が乗るのを防ぐ。
float4 ObjectToonApplyZOffset(float4 positionCS, float viewSpaceZOffset)
{
    if (unity_OrthoParams.w == 0)
    {
        float2 projZW = UNITY_MATRIX_P[2].zw;
        float modifiedVSZ = -positionCS.w - viewSpaceZOffset;
        float modifiedCSZ = modifiedVSZ * projZW[0] + projZW[1];
        positionCS.z = modifiedCSZ * positionCS.w / (-modifiedVSZ);
        return positionCS;
    }

    positionCS.z += -viewSpaceZOffset / _ProjectionParams.z;
    return positionCS;
}

#endif
