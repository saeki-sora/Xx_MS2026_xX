// 地面(XY平面, z=0)に寝ている平らな絵を、描画中のカメラに向けて「立たせる」計算。
// BillboardSprite.shader(砲台・コア・破壊可能物)と SwarmSprite.shader(群衆)で共有する。
//
// 考え方: 頂点のずれを「画面の右」成分と「画面の上」成分に分け、上成分だけを地面からカメラ側へ起こす。
// 立ち上がり(stand)=0 なら元の位置と完全に一致するので、真上視点やOFF時に見た目が変わらない。
// カメラごとに計算するので、4人の視点の回転が違っても、それぞれの画面で正面を向く。
#ifndef FORTRESS_BILLBOARD_INCLUDED
#define FORTRESS_BILLBOARD_INCLUDED

// 1で足元(絵の下端)を地面に固定、0で絵の中心を軸に立てる。
float _FortressBillboardFeet;

// 地面上での「画面の上」方向(単位ベクトル)。
float2 FortressGroundUp()
{
    float3 cameraUp = UNITY_MATRIX_V[1].xyz;
    float length2 = length(cameraUp.xy);
    if (length2 > 1e-4)
    {
        return cameraUp.xy / length2;
    }

    // 真横から見ているなどで上方向が地面に落ちないときは、カメラの右方向から求める。
    float3 cameraRight = UNITY_MATRIX_V[0].xyz;
    return normalize(float2(-cameraRight.y, cameraRight.x) + 1e-6);
}

// offsetWS        : 基準点から見た頂点のずれ(ワールド, 地面上)
// boundsCenterWS  : 基準点から見た絵の外形の中心(ワールド, 地面上)
// boundsAxisXWS/Y : 絵の外形の半径ベクトル2本(ワールド, 地面上)。足元の位置を求めるのに使う
// stand           : 立ち上がり具合(0=寝たまま 1=カメラに正対)
// 戻り値          : 基準点から見た、立たせた後の頂点のずれ(ワールド)
float3 FortressBillboardOffset(float2 offsetWS, float2 boundsCenterWS, float2 boundsAxisXWS, float2 boundsAxisYWS, float stand)
{
    float2 groundUp = FortressGroundUp();
    float2 groundRight = float2(groundUp.y, -groundUp.x);

    // カメラの上方向を、立ち上がり具合に応じて地面から起こした向き(z<0がカメラ側)。
    float3 cameraUp = UNITY_MATRIX_V[1].xyz;
    float3 standUp = normalize(float3(groundUp * max(length(cameraUp.xy), 1e-4), cameraUp.z * stand));

    float right = dot(offsetWS, groundRight);
    float up = dot(offsetWS, groundUp);

    // 画面の上方向で測った絵の下端。足元固定ならここを軸に起こす。
    float bottom = dot(boundsCenterWS, groundUp) - abs(dot(boundsAxisXWS, groundUp)) - abs(dot(boundsAxisYWS, groundUp));
    float pivotUp = bottom * _FortressBillboardFeet;

    return float3(groundRight * right + groundUp * pivotUp, 0.0) + standUp * (up - pivotUp);
}

#endif
