using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 群衆の見た目の演出をまとめて出す（<see cref="SwarmSystem"/> が Play 開始時に自動で付ける）。どれも見た目だけで、通信はしない。
    /// ・倒したときの光の粒（群衆の設定「倒したときの光の粒」）: 倒した敵が燃え尽きるように光り、倒した人の色の粒になって散ってから、その人の砲台へ吸い込まれる。
    ///   <see cref="SwarmSystem.AgentsDied"/> を使う（Client では Host から「倒された」と届いた敵。倒した人は自分のPCで見えていたビームから決める）。
    /// ・爆弾の閃光と衝撃波（敵の種類の「爆弾」設定）: <see cref="SwarmSystem.BombsExploded"/> を使う（全PC）。
    /// 既存の爆発の演出（エフェクト・効果音・範囲の輪）とは別に重ねて出す。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SwarmSystem))]
    public sealed class SwarmFxDirector : MonoBehaviour
    {
        private const int MaxShockwaves = 24;
        private const float SparkLife = 2.5f;
        private const float DeathFlashLife = 0.18f;
        private const float TurretScanInterval = 1f;

        private SwarmSystem _swarm;
        private SwarmFxParticles _particles;
        private BombShockwaves _shockwaves;
        private readonly Vector2?[] _targets = new Vector2?[BeamLightGlobals.SlotCount];
        private readonly List<LaserTurret> _turrets = new List<LaserTurret>();
        private float _nextTurretScan;
        private int _spawnedThisFrame;
        private int _particleCapacity;

        private void Awake()
        {
            _swarm = GetComponent<SwarmSystem>();
        }

        private void OnEnable()
        {
            _swarm.AgentsDied += HandleDeaths;
            _swarm.BombsExploded += HandleExplosions;
        }

        private void OnDisable()
        {
            _swarm.AgentsDied -= HandleDeaths;
            _swarm.BombsExploded -= HandleExplosions;
            _particles?.Clear();
        }

        private void OnDestroy()
        {
            _particles?.Dispose();
            _particles = null;
            _shockwaves?.Dispose();
            _shockwaves = null;
        }

        private void LateUpdate()
        {
            var settings = _swarm.ActiveSettings;
            var dt = Time.deltaTime;
            if (_particles != null && settings != null)
            {
                RefreshTargets();
                _particles.Update(dt, _targets, settings.sparkHomingDelay, settings.sparkHomingSpeed);
                _particles.Render();
            }

            _shockwaves?.Update(dt);
            _spawnedThisFrame = 0;
        }

        private void HandleDeaths(IReadOnlyList<SwarmDeath> deaths)
        {
            var settings = _swarm.ActiveSettings;
            if (settings == null || !settings.killSparks || !EnsureParticles(settings))
            {
                return;
            }

            for (var i = 0; i < deaths.Count; i++)
            {
                if (_spawnedThisFrame >= settings.maxNewSparksPerFrame)
                {
                    return;
                }

                var death = deaths[i];
                var position = new Vector2(death.position.x, death.position.y);
                var color = DeathColor(death.owner, settings.sparkBrightness);
                var definition = _swarm.GetRegisteredType(death.type);
                var spriteSize = definition != null && definition.swarm != null ? definition.swarm.spriteSize : 0.8f;

                // 燃え尽きるような光（白っぽく、すぐ消える）。
                if (settings.deathFlashSize > 0f && _particles.AddFlash(position, spriteSize * settings.deathFlashSize,
                        Color.Lerp(Color.white * settings.sparkBrightness, color, 0.5f), DeathFlashLife))
                {
                    _spawnedThisFrame++;
                }

                for (var s = 0; s < settings.sparksPerKill && _spawnedThisFrame < settings.maxNewSparksPerFrame; s++)
                {
                    var angle = Random.value * Mathf.PI * 2f;
                    var speed = settings.sparkBurstSpeed * Random.Range(0.5f, 1f);
                    var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                    var owner = death.owner >= 0 && death.owner < _targets.Length ? death.owner : -1;
                    // 倒した人が分からない粒は、その場で散って消える（砲台へは飛ばない）。
                    var life = owner >= 0 ? SparkLife : 0.6f;
                    if (_particles.AddSpark(position, velocity, settings.sparkSize * Random.Range(0.8f, 1.2f), color, owner, life))
                    {
                        _spawnedThisFrame++;
                    }
                }
            }
        }

        private void HandleExplosions(IReadOnlyList<SwarmExplosion> explosions)
        {
            var settings = _swarm.ActiveSettings;
            for (var i = 0; i < explosions.Count; i++)
            {
                var explosion = explosions[i];
                var definition = _swarm.GetRegisteredType(explosion.type);
                var bomb = definition != null ? definition.bomb : null;
                if (bomb == null)
                {
                    continue;
                }

                var position = new Vector2(explosion.position.x, explosion.position.y);
                if (bomb.flash && settings != null && EnsureParticles(settings))
                {
                    _particles.AddFlash(position, bomb.explosionRadius * bomb.flashSize * 2f, bomb.flashColor, bomb.flashSeconds);
                }

                if (bomb.shockwave && bomb.shockwaveStrength > 0f)
                {
                    _shockwaves ??= new BombShockwaves(MaxShockwaves);
                    var tint = new Color(bomb.flashColor.r, bomb.flashColor.g, bomb.flashColor.b, 0.25f);
                    _shockwaves.Spawn(position, bomb.explosionRadius, bomb.shockwaveStrength, bomb.shockwaveSeconds, tint);
                }
            }
        }

        private Color DeathColor(int owner, float brightness)
        {
            var color = owner >= 0 && owner < _targets.Length
                ? LaserLightMath.Saturate(FortressColors.PlayerColor(owner), 1.2f)
                : new Color(1f, 0.95f, 0.85f);
            return color * brightness;
        }

        private bool EnsureParticles(SwarmSettings settings)
        {
            // 上限を変えたら作り直す（Play中に設定を変えたとき）。
            if (_particles != null && _particleCapacity == settings.maxSparks)
            {
                return true;
            }

            _particles?.Dispose();
            _particleCapacity = settings.maxSparks;
            _particles = new SwarmFxParticles(_particleCapacity);
            return true;
        }

        private void RefreshTargets()
        {
            if (Time.time >= _nextTurretScan)
            {
                _nextTurretScan = Time.time + TurretScanInterval;
                _turrets.Clear();
                _turrets.AddRange(FindObjectsByType<LaserTurret>(FindObjectsSortMode.None));
            }

            for (var p = 0; p < _targets.Length; p++)
            {
                _targets[p] = null;
            }

            for (var i = 0; i < _turrets.Count; i++)
            {
                var turret = _turrets[i];
                if (turret != null && turret.isActiveAndEnabled && turret.playerIndex >= 0 && turret.playerIndex < _targets.Length)
                {
                    var position = turret.transform.position;
                    _targets[turret.playerIndex] = new Vector2(position.x, position.y);
                }
            }
        }
    }
}
