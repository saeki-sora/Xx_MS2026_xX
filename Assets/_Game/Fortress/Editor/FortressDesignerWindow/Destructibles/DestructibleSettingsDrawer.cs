using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 破壊可能物の全設定項目を、分かりやすいセクションに分けて描く。タブの詳細編集とInspectorの両方で共用する。
    /// SerializedObjectは複数の対象をまとめて渡せるので、複数選択での一括編集もそのまま動く。
    /// </summary>
    public static class DestructibleSettingsDrawer
    {
        private readonly struct Field
        {
            public readonly string Path;
            public readonly string Label;

            /// <summary>このboolがOFFのとき、この項目は隠す（空なら常に表示）。</summary>
            public readonly string ShownWhen;

            public Field(string path, string label, string shownWhen = null)
            {
                Path = path;
                Label = label;
                ShownWhen = shownWhen;
            }
        }

        private sealed class Section
        {
            public string Title;
            public bool OpenByDefault;
            public Field[] Fields;
        }

        private const string Dur = "settings.durability.";
        private const string Vis = "settings.visual.";
        private const string Bar = "settings.visual.healthBar.";
        private const string Fb = "settings.feedback.";
        private const string Drop = "settings.drops.";
        private const string Col = "settings.collision.";

        private static readonly Section[] Sections =
        {
            new Section
            {
                Title = "耐久・再生", OpenByDefault = true,
                Fields = new[]
                {
                    new Field(Dur + "maxHealth", "耐久値"),
                    new Field(Dur + "damageMultiplier", "受けるダメージの倍率"),
                    new Field(Dur + "selfRepairPerSecond", "自己修復の速さ(耐久/秒)"),
                    new Field(Dur + "selfRepairDelaySeconds", "自己修復が始まるまで(秒)"),
                    new Field(Dur + "regenerates", "壊れても再生する"),
                    new Field(Dur + "regenDelaySeconds", "再生までの時間(秒)", Dur + "regenerates"),
                    new Field(Dur + "regenHealthRatio", "再生時の耐久の割合", Dur + "regenerates")
                }
            },
            new Section
            {
                Title = "見た目（絵の差し替え）", OpenByDefault = true,
                Fields = new[]
                {
                    new Field(Vis + "visualPrefab", "通常の見た目Prefab（2D/3D）"),
                    new Field(Vis + "fitMode", "大きさへの合わせ方"),
                    new Field(Vis + "visualOffset", "見た目の位置ずらし"),
                    new Field(Vis + "sortingOrderOffset", "描画順の加算"),
                    new Field(Vis + "damageStages", "ダメージ段階の見た目"),
                    new Field(Vis + "darkenWithDamage", "ダメージで暗くする"),
                    new Field(Vis + "destroyedPrefab", "壊れた後の見た目Prefab"),
                    new Field(Vis + "destroyedGhostAlpha", "壊れた後の「跡」の濃さ"),
                    new Field(Vis + "controlPlaceholderColor", "仮の四角の色を上書きする"),
                    new Field(Vis + "placeholderColor", "仮の四角の色", Vis + "controlPlaceholderColor")
                }
            },
            new Section
            {
                Title = "被ダメージの反応・耐久ゲージ", OpenByDefault = false,
                Fields = new[]
                {
                    new Field(Vis + "hitFlashSeconds", "被弾で光る長さ(秒)"),
                    new Field(Vis + "hitFlashColor", "光る色"),
                    new Field(Vis + "hitShake", "被弾で震える大きさ"),
                    new Field(Bar + "mode", "耐久ゲージの表示"),
                    new Field(Bar + "width", "ゲージの長さ(0=横幅に合わせる)"),
                    new Field(Bar + "height", "ゲージの太さ"),
                    new Field(Bar + "offsetY", "ゲージの高さ位置"),
                    new Field(Bar + "fillColor", "ゲージの色"),
                    new Field(Bar + "backColor", "ゲージの背景色")
                }
            },
            new Section
            {
                Title = "演出（エフェクト・効果音）", OpenByDefault = false,
                Fields = new[]
                {
                    new Field(Fb + "onHit", "被ダメージ時"),
                    new Field(Fb + "hitInterval", "被ダメージ演出の間隔(秒)"),
                    new Field(Fb + "onStageChanged", "見た目の段階が進んだとき"),
                    new Field(Fb + "onDestroyed", "破壊されたとき"),
                    new Field(Fb + "onRegenerated", "再生したとき"),
                    new Field(Fb + "effectLifetimeSeconds", "エフェクトを消すまで(秒)"),
                    new Field(Fb + "placeholderDebris", "仮の破片を出す（破壊時の演出が空のとき）"),
                    new Field(Fb + "debrisCount", "仮の破片の数", Fb + "placeholderDebris"),
                    new Field(Fb + "debrisColor", "仮の破片の色", Fb + "placeholderDebris")
                }
            },
            new Section
            {
                Title = "ドロップ", OpenByDefault = false,
                Fields = new[]
                {
                    new Field(Drop + "entries", "落とすもの"),
                    new Field(Drop + "scatterRadius", "散らばる半径"),
                    new Field(Drop + "onlyFirstDestruction", "最初の破壊のときだけ落とす")
                }
            },
            new Section
            {
                Title = "当たり判定・敵の経路", OpenByDefault = false,
                Fields = new[]
                {
                    new Field(Col + "shape", "当たり判定の形"),
                    new Field(Col + "blocksEnemies", "壊れていない間、敵を通さない（迂回させる）"),
                    new Field(Col + "leavesRubble", "壊れた後に瓦礫（通行コスト地帯）を残す"),
                    new Field(Col + "rubbleSpeedMultiplier", "瓦礫の上の移動速度倍率", Col + "leavesRubble"),
                    new Field(Col + "rubbleCostMultiplier", "瓦礫の通行コスト倍率", Col + "leavesRubble")
                }
            },
            new Section
            {
                Title = "連動（この個体のみ）", OpenByDefault = false,
                Fields = new[]
                {
                    new Field("link.groupId", "グループ名（同じ名前で連動）"),
                    new Field("link.groupMode", "グループの連動のしかた"),
                    new Field("link.chainRadius", "連鎖の半径（0=連鎖しない）"),
                    new Field("link.chainDelaySeconds", "連鎖までの遅れ(秒)"),
                    new Field("link.chainDamage", "連鎖で与えるダメージ")
                }
            },
            new Section
            {
                Title = "無敵条件（この個体のみ）", OpenByDefault = false,
                Fields = new[]
                {
                    new Field("protection.startsInvulnerable", "開始時は無敵"),
                    new Field("protection.unlockAfterSeconds", "開始からこの秒数で解除(0=時間では解除しない)", "protection.startsInvulnerable"),
                    new Field("protection.unlockWhenDestroyed", "これらが全て壊れたら解除", "protection.startsInvulnerable")
                }
            }
        };

        public static void Draw(SerializedObject serialized)
        {
            serialized.Update();

            foreach (var section in Sections)
            {
                var key = "Fortress.Destructible.Section." + section.Title;
                var open = SessionState.GetBool(key, section.OpenByDefault);
                open = EditorGUILayout.Foldout(open, section.Title, true, EditorStyles.foldoutHeader);
                SessionState.SetBool(key, open);
                if (!open)
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                foreach (var field in section.Fields)
                {
                    DrawField(serialized, field);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            serialized.ApplyModifiedProperties();
        }

        private static void DrawField(SerializedObject serialized, Field field)
        {
            if (field.ShownWhen != null)
            {
                var gate = serialized.FindProperty(field.ShownWhen);
                if (gate != null && !gate.boolValue && !gate.hasMultipleDifferentValues)
                {
                    return;
                }
            }

            var property = serialized.FindProperty(field.Path);
            if (property == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(field.Label, property.tooltip), true);
        }
    }
}
