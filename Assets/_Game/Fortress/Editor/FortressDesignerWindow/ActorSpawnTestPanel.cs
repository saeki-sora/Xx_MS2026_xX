using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress.Net;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「テスト」タブの一部。敵の種類を選んで、Actor(1体ごとにGameObjectを持つ敵。ボスなど)として湧かせる。
    /// ウェーブ設定や敵の種類の設定は変えずに、Actorの動き・ネット対戦での同期を確かめるためのもの。
    /// </summary>
    public sealed class ActorSpawnTestPanel
    {
        private EnemyTypeDefinition _type;
        private int _count = 1;

        public void Draw()
        {
            EditorGUILayout.LabelField("Actorの敵を湧かせる（テスト）", EditorStyles.boldLabel);

            var director = Object.FindFirstObjectByType<EnemySpawnDirector>();
            if (director == null)
            {
                EditorGUILayout.HelpBox("シーンにEnemySpawnDirectorが見つかりません。", MessageType.Warning);
                return;
            }

            var waveTypes = WaveTypes(director);
            if (_type == null)
            {
                _type = waveTypes.FirstOrDefault();
            }

            _type = (EnemyTypeDefinition)EditorGUILayout.ObjectField(
                new GUIContent("敵の種類", "湧かせる敵の種類。群衆モードの種類でもActorとして湧く（種類の設定は変わらない）。"),
                _type, typeof(EnemyTypeDefinition), false);
            _count = EditorGUILayout.IntSlider(new GUIContent("数", "一度に湧かせる数。湧き位置のどれかにランダムに出る。"), _count, 1, 20);

            var isClient = FortressNet.IsNetworked && !FortressNet.HasSimulationAuthority;
            if (isClient)
            {
                EditorGUILayout.HelpBox("ネット対戦のClientでは湧かせられません（敵はHostが湧かせて届けます）。Host側で押してください。", MessageType.Info);
            }
            else if (FortressNet.IsNetworked && _type != null && !waveTypes.Contains(_type))
            {
                EditorGUILayout.HelpBox("この種類はウェーブ設定に入っていないため、Clientの画面には表示されません。" +
                                        "ウェーブに入っている種類を選ぶか、ウェーブに追加してください。", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(isClient || _type == null))
                {
                    if (GUILayout.Button("Actorとして湧かせる"))
                    {
                        for (var i = 0; i < _count; i++)
                        {
                            director.SpawnActorForTest(_type);
                        }
                    }

                    if (GUILayout.Button("Actorの敵を全て消す"))
                    {
                        foreach (var enemy in EnemyController.Active.Where(e => !e.IsReplica).ToArray())
                        {
                            Object.Destroy(enemy.gameObject);
                        }
                    }
                }
            }

            var replicas = EnemyController.Active.Count(e => e.IsReplica);
            EditorGUILayout.LabelField("いるActorの敵",
                replicas > 0 ? $"{EnemyController.Active.Count}体（うちHostから届いた表示 {replicas}体）" : $"{EnemyController.Active.Count}体");
        }

        private static List<EnemyTypeDefinition> WaveTypes(EnemySpawnDirector director)
        {
            if (director.wave == null || director.wave.spawnEntries == null)
            {
                return new List<EnemyTypeDefinition>();
            }

            return director.wave.spawnEntries
                .Where(e => e != null && e.enemyType != null)
                .Select(e => e.enemyType)
                .Distinct()
                .ToList();
        }
    }
}
