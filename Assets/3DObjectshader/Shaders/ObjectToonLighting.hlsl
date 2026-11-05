// トゥーンの光の当たり方（見た目の大部分はここで決まる）。
// 元: SimpleURPToonLitOutlineExample_LightingEquation.hlsl（ColinLeung-NiloCat, MIT。ThirdPartyNotices を参照）。
// 変更点: 背景の物向けに、太陽の明るさを無視する量・光の色の影響・ハイライト・ふちの光・彩度/明るさを足した。
//         オクルージョン・顔用の処理は使わないので外した（テクスチャを読む回数を減らす）。
// 機能のオンオフはキーワードを増やさず材質の値（全画素で同じ分岐）で切り替える（シェーダーの種類を増やさない）。
#ifndef MS2026_OBJECT_TOON_LIGHTING_INCLUDED
#define MS2026_OBJECT_TOON_LIGHTING_INCLUDED

struct ObjectToonSurface
{
    half3  albedo;
    half3  emission;
    half3  normalWS;
    half3  viewDirWS;
    float3 positionWS;
    float4 positionCS;
    float2 uv;
};

// 鏡の映り込み: 映る向きが画面の上なら「空」、下なら「床」の色。段の数で絵の具っぽく塗り分ける。
// _ReflectEnvBlend > 0 のときだけ、まわりの景色（反射の元の画像）を1回読んで混ぜる。
half3 ObjectToonReflection(half3 N, half3 V)
{
    half3 R = reflect(-V, N);
    half up = TransformWorldToViewDir(R, true).y; // 画面の上 = +1
    if (_ReflectSteps >= 1.0h)
    {
        // 段ごとに、その段のまん中の値にそろえる
        half band = saturate(up * 0.5h + 0.5h);
        band = (min(floor(band * _ReflectSteps), _ReflectSteps - 1.0h) + 0.5h) / _ReflectSteps;
        up = band * 2.0h - 1.0h;
    }

    half3 color = up >= 0.0h
        ? lerp(_ReflectHorizonColor.rgb, _ReflectSkyColor.rgb, saturate(up))
        : lerp(_ReflectHorizonColor.rgb, _ReflectGroundColor.rgb, saturate(-up));

    if (_ReflectEnvBlend > 0.0h)
    {
        half4 encoded = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, R, 0);
        color = lerp(color, DecodeHDREnvironment(encoded, unity_SpecCube0_HDR), _ReflectEnvBlend);
    }

    return color;
}

// 斜めの光の筋（太い1本＋細い1本）。0〜1。
half ObjectToonStreak(float2 uv, float4 positionCS)
{
    float2 coord = uv;
    if (_StreakScreenSpace > 0.5h)
    {
        coord = GetNormalizedScreenSpaceUV(positionCS);
        coord.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
    }

    float angle = radians(_StreakAngle);
    float s = dot(coord, float2(cos(angle), sin(angle))) - _StreakOffset;
    half soft = max(_StreakSoftness * 0.1h, 1e-4h);
    half wide = 1.0h - smoothstep(_StreakWidth - soft, _StreakWidth + soft, abs(s));
    half thin = 1.0h - smoothstep(_StreakWidth * 0.4h - soft, _StreakWidth * 0.4h + soft, abs(s - _StreakGap));
    return saturate(wide + thin);
}

// 明暗の境目。0 = 影、1 = 光。
half ObjectToonCel(half NoL)
{
    half soft = max(_CelSoftness, 1e-3h);
    return smoothstep(_CelMidPoint - soft, _CelMidPoint + soft, NoL);
}

// 光の色。_IgnoreLightIntensity = 1 で光の強さを無視して色味だけにする（暗い太陽でも明るい面がはっきり出る）。
half3 ObjectToonLightColor(half3 lightColor)
{
    half peak = max(max(lightColor.r, lightColor.g), max(lightColor.b, 1e-3h));
    half3 color = lerp(saturate(lightColor), lightColor / peak, _IgnoreLightIntensity);
    half luma = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
    return lerp(luma.xxx, color, _LightColorInfluence);
}

half3 ObjectToonGI(half3 normalWS)
{
    // 細かい陰影を消して平らな色に寄せる（_IndirectFlatten = 1 で向きに関係なく同じ色）。
    half3 sh = lerp(SampleSH(normalWS), SampleSH(half3(0, 0, 0)), _IndirectFlatten);
    return max(_IndirectMinColor.rgb, sh) * _IndirectStrength;
}

half3 ObjectToonShade(ObjectToonSurface s)
{
    half3 N = s.normalWS;
    half3 V = s.viewDirWS;

    // ── 太陽 ──
    Light mainLight = GetMainLight();
#if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    float3 shadowTestPositionWS = s.positionWS + mainLight.direction * _ReceiveShadowOffset;
    mainLight.shadowAttenuation = MainLightRealtimeShadow(TransformWorldToShadowCoord(shadowTestPositionWS));
#endif

    half mainLit = ObjectToonCel(dot(N, mainLight.direction));
    mainLit *= lerp(1.0h, mainLight.shadowAttenuation, _ReceiveShadowStrength);
    half3 direct = lerp(_ShadowColor.rgb, 1.0h, mainLit) * ObjectToonLightColor(mainLight.color) * _DirectStrength;

    // ── 追加の光（点光源・スポット・倒したときの光）──
    // 元は光の色を1で頭打ちにしていたが、倒したときの「パッ」と光る強い光が弱まるので、4で頭打ちにした。
#if defined(_ADDITIONAL_LIGHTS)
    if (_AdditionalLightsOn > 0.5h)
    {
        half3 additional = 0;
        InputData inputData = (InputData)0;
        inputData.positionWS = s.positionWS;
        inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(s.positionCS);
        uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
        {
            Light light = GetAdditionalLight(lightIndex, s.positionWS);
            half lit = lerp(ObjectToonCel(dot(N, light.direction)), 1.0h, _AdditionalLightCelBlend);
            additional += min(light.color, 4.0h) * min(4.0h, light.distanceAttenuation) * lit;
        }
    #endif

        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light light = GetAdditionalLight(lightIndex, s.positionWS);
            half lit = lerp(ObjectToonCel(dot(N, light.direction)), 1.0h, _AdditionalLightCelBlend);
            additional += min(light.color, 4.0h) * min(4.0h, light.distanceAttenuation) * lit;
        LIGHT_LOOP_END

        direct += additional * _AdditionalLightStrength;
    }
#endif

    // ── まわりの光と合わせる（元と同じく「明るい方を選ぶ」。_LightAddBlend で足し算に寄せられる）──
    half3 indirect = ObjectToonGI(N);
    half3 lightSum = lerp(max(indirect, direct), indirect + direct, _LightAddBlend);
    half3 color = s.albedo * lightSum;

    // ── 鏡・つや（映り込みに置き換える。ふちほど強く映すこともできる）──
    if (_ReflectOn > 0.5h)
    {
        half edge = 1.0h - saturate(dot(N, V));
        half amount = _ReflectStrength * lerp(1.0h, saturate(edge + 0.35h), _ReflectFresnel);
        color = lerp(color, ObjectToonReflection(N, V) * s.albedo, amount);
    }

    // ── ハイライト（くっきりした光の点）──
    if (_SpecularOn > 0.5h)
    {
        half3 H = SafeNormalize(mainLight.direction + V);
        half NoH = saturate(dot(N, H));
        half threshold = 1.0h - _SpecularSize * 0.2h;
        half soft = max(_SpecularSoftness * 0.05h, 1e-4h);
        half spec = smoothstep(threshold - soft, threshold + soft, NoH) * mainLit;
        color += _SpecularColor.rgb * spec;
    }

    // ── ふちの光 ──
    if (_RimOn > 0.5h)
    {
        half edge = 1.0h - saturate(dot(N, V));
        half start = 1.0h - _RimWidth;
        half soft = max(_RimSoftness * 0.5h, 1e-3h);
        half rim = smoothstep(start - soft, start + soft, edge);
        rim *= lerp(1.0h, mainLit, _RimLitSideOnly);
        color += _RimColor.rgb * rim;
    }

    // ── 鏡の斜めの光の筋 ──
    if (_StreakOn > 0.5h)
    {
        color += _StreakColor.rgb * ObjectToonStreak(s.uv, s.positionCS);
    }

    // ── ビームが照らす光（ステージの物と同じ）・自己発光 ──
    color += FortressBeamSurfaceLight(s.positionWS, N, s.albedo) * _BeamLightStrength;
    color += s.emission;

    return color;
}

// 仕上げ: 彩度と明るさ。
half3 ObjectToonGrade(half3 color)
{
    half luma = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
    return max(0.0h, lerp(luma.xxx, color, _Saturation)) * _Brightness;
}

#endif
