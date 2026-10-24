using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// ウェーブの発表で、その試合で初めて出てくる敵を「NEW ENEMY」のカードで紹介する（2026-10-24、初めての人向け）。
    /// どの敵が初登場かは <see cref="WaveIntro.NewTypes"/>（紹介文 <see cref="EnemyTypeDefinition.introText"/> のある敵だけ）。
    /// カードの絵は introIcon、無ければ群衆の絵の1コマ目、それも無ければ仮絵の色の丸。
    /// UIスタジオの「ウェーブの表示を入れる」が作る「WaveNewEnemies」に付く（出し入れは wave.announcing）。
    /// </summary>
    [AddComponentMenu("UI Studio/初登場の敵のカード (WaveNewEnemyCards)")]
    public sealed class WaveNewEnemyCards : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Card
        {
            public GameObject root;
            public RawImage picture;
            public Image fallback;
            public Text title;
            public Text body;
        }

        public Card[] cards = new Card[0];

        private int _shownWave = -2;
        private WavePlan _shownPlan;

        private void Update()
        {
            var announcing = FortressWaves.IsActive && FortressWaves.Phase == WavePhase.Announce;
            var plan = FortressWaves.Plan;
            var wave = announcing ? FortressWaves.WaveIndex : -1;
            if (wave == _shownWave && plan == _shownPlan)
            {
                return;
            }

            _shownWave = wave;
            _shownPlan = plan;
            var types = announcing && plan != null ? WaveIntro.NewTypes(plan.waves, wave) : new List<EnemyTypeDefinition>();
            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card?.root == null)
                {
                    continue;
                }

                var type = i < types.Count ? types[i] : null;
                card.root.SetActive(type != null);
                if (type != null)
                {
                    Fill(card, type);
                }
            }
        }

        private static void Fill(Card card, EnemyTypeDefinition type)
        {
            if (card.title != null)
            {
                card.title.text = string.IsNullOrEmpty(type.displayName) ? type.name : type.displayName;
            }

            if (card.body != null)
            {
                card.body.text = type.introText;
            }

            Texture texture = null;
            var uv = new Rect(0f, 0f, 1f, 1f);
            if (type.introIcon != null)
            {
                texture = type.introIcon.texture;
                var r = type.introIcon.textureRect;
                uv = new Rect(r.x / texture.width, r.y / texture.height, r.width / texture.width, r.height / texture.height);
            }
            else if (type.swarm != null && type.swarm.spriteSheet != null)
            {
                // 群衆の絵: 横＝コマ、縦＝向き（一番上の段＝右向き）。1コマ目の右向き。
                texture = type.swarm.spriteSheet;
                var columns = Mathf.Max(1, type.swarm.frameCount);
                var rows = Mathf.Max(1, type.swarm.directionCount);
                uv = new Rect(0f, 1f - 1f / rows, 1f / columns, 1f / rows);
            }

            if (card.picture != null)
            {
                card.picture.texture = texture;
                card.picture.uvRect = uv;
                card.picture.enabled = texture != null;
            }

            if (card.fallback != null)
            {
                card.fallback.enabled = texture == null;
                card.fallback.color = type.placeholderColor;
            }
        }
    }
}
