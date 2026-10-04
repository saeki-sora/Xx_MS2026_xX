using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// Actorモードの敵(1体ごとにGameObjectを持つ敵)を生成する。ウェーブ再生・ネット対戦のClient側の表示用・
    /// テスト用の「湧かせる」ボタンが、全て同じ作り方になるようにまとめている。
    /// </summary>
    public static class EnemyActorFactory
    {
        /// <param name="type">敵の種類。群衆(Swarm)モードの種類を渡しても、Actorとして生成する。</param>
        /// <param name="fallbackPrefab">種類に見た目のPrefabが無いときに使う土台。nullなら空のGameObjectから作る。</param>
        public static EnemyController Create(EnemyTypeDefinition type, Vector3 position, GameObject fallbackPrefab)
        {
            var prefab = type.visualPrefab != null ? type.visualPrefab : fallbackPrefab;
            var instance = prefab != null
                ? Object.Instantiate(prefab, position, Quaternion.identity)
                : new GameObject(type.displayName);

            instance.transform.position = position;

            var enemy = instance.GetComponent<EnemyController>();
            if (enemy == null)
            {
                enemy = instance.AddComponent<EnemyController>();
            }

            enemy.definition = type;
            return enemy;
        }
    }
}
