using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    // ScriptableObject を作るので Unity のテストランナーで動かす。
    public sealed class WaveIntroTests
    {
        [Test]
        public void OnlyTypesNotSeenInEarlierWavesAndWithAnIntro()
        {
            var spiki = ScriptableObject.CreateInstance<EnemyTypeDefinition>();
            spiki.introText = "まっすぐコアへ来る";
            var bomb = ScriptableObject.CreateInstance<EnemyTypeDefinition>();
            bomb.introText = "倒すと爆発！";
            var silent = ScriptableObject.CreateInstance<EnemyTypeDefinition>();
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition { spawnEntries = { new EnemySpawnEntry { enemyType = spiki, count = 1 } } },
                new WaveDefinition
                {
                    spawnEntries =
                    {
                        new EnemySpawnEntry { enemyType = spiki, count = 1 },
                        new EnemySpawnEntry { enemyType = bomb, count = 1 },
                        new EnemySpawnEntry { enemyType = silent, count = 1 }
                    }
                }
            };

            CollectionAssert.AreEqual(new[] { spiki }, WaveIntro.NewTypes(waves, 0));
            CollectionAssert.AreEqual(new[] { bomb }, WaveIntro.NewTypes(waves, 1), "スピキは前に出た・紹介文の無い種類は出さない");
            Assert.IsEmpty(WaveIntro.NewTypes(waves, 5));

            Object.DestroyImmediate(spiki);
            Object.DestroyImmediate(bomb);
            Object.DestroyImmediate(silent);
        }
    }
}
