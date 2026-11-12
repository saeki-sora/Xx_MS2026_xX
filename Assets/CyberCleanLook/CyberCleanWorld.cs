using System.Collections.Generic;
using System.Text;
using MS2026.Fortress;
using MS2026.Stage;
using MS2026.Stages;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.CyberCleanLook
{
    /// <summary>
    /// 電脳クリーニングの見た目の係。ステージが当てはめられたら（<see cref="StageLayout.Applied"/>）、そのステージのシーンに自動で作り、ステージと一緒に消える。
    /// ・床と床の外の色: Play 中だけの材質の複製に色を入れる（材質のファイルは書き換えない。消えるときに元へ戻す）
    /// ・光: ステージの光の設定を写して色を変え、太陽とまわりの明るさに入れる（ステージの設定のファイルは書き換えない）
    /// ・靄: 毎フレーム、描く直前に色を変えた靄の設定を入れる（共通の設定のファイルは書き換えない）
    /// ・WAVE の画面演出は <see cref="CyberCleanScreenFx"/>
    /// どれも見た目だけで、通信もしない。
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class CyberCleanWorld : MonoBehaviour
    {
        private const float ReapplySeconds = 1f;

        private static CyberCleanWorld _current;

        private readonly List<(Renderer Renderer, Material[] Original)> _recolored = new List<(Renderer, Material[])>();
        private readonly List<(Material Copy, Color Source, bool Outer, bool Multiply)> _copies = new List<(Material, Color, bool, bool)>();
        private CyberCleanSettings _settings;
        private EdgeMistSettings _mist;
        private EdgeMistSettings _mistSource;
        private float _reapplyTimer;
        private bool _mistDirty = true;
        private bool _floorDone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            StageLayout.Applied -= HandleApplied;
            _current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            StageLayout.Applied -= HandleApplied;
            StageLayout.Applied += HandleApplied;
            if (StageLayout.Active != null && StageLayout.Active.IsApplied)
            {
                HandleApplied(StageLayout.Active);
            }
        }

        private static void HandleApplied(StageLayout layout)
        {
            if (_current != null || layout == null || !Application.isPlaying)
            {
                return;
            }

            var settings = CyberCleanSettings.Load();
            var scene = layout.gameObject.scene;
            if (!settings.IsStage(scene.name))
            {
                return;
            }

            var go = new GameObject("[Stage] CyberCleanLook");
            SceneManager.MoveGameObjectToScene(go, scene);
            _current = go.AddComponent<CyberCleanWorld>();
            _current._settings = settings;
            go.AddComponent<CyberCleanScreenFx>().Init(settings);
        }

        private void OnDestroy()
        {
            RestoreFloor();
            if (_current == this)
            {
                _current = null;
            }
        }

        private void LateUpdate()
        {
            var s = _settings;
            if (!_floorDone && s.recolorFloor && StageProp.All.Count > 0)
            {
                RecolorFloor();
            }
            else if (_floorDone && !s.recolorFloor)
            {
                RestoreFloor();
            }

            // ステージの光は、ステージが開いたときや、ステージのシーンが前に出たときに入れ直されるので、1秒ごとにこちらの光に戻す。
            _reapplyTimer -= Time.unscaledDeltaTime;
            if (_reapplyTimer <= 0f)
            {
                _reapplyTimer = ReapplySeconds;
                if (s.overrideLight)
                {
                    ApplyLight();
                }

                ApplyFloorColors();

                // 靄の形・濃さは共通の設定に合わせるので、1秒ごとに写し直す（毎フレーム写すと小さなゴミが出る）。
                _mistDirty = true;
            }

            // 靄の設定は毎フレーム共通の設定で入れ直されるので、描く直前（一番最後の LateUpdate）に色を変えた物を入れる。
            if (s.recolorMist)
            {
                ApplyMist();
            }
        }

        private void ApplyLight()
        {
            var root = StageRoot.Active;
            if (root == null || root.current == null)
            {
                return;
            }

            var s = _settings;
            var light = new StageLightingSettings();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(root.current.lighting), light);
            light.apply = true;
            light.sunColor = s.sunColor;
            light.sunIntensity = s.sunIntensity;
            light.sunDirection = s.sunDirection;
            light.sunElevation = s.sunElevation;
            light.shadowStrength = s.shadowStrength;
            light.skyColor = s.skyColor;
            light.equatorColor = s.equatorColor;
            light.groundColor = s.groundColor;
            if (s.cinematicToneMapping)
            {
                light.toneMapping = StageToneMapping.Cinematic;
            }

            light.ApplyTo(root.EnsureSun());
            StageLookVolume.Apply(root, light);
        }

        private void ApplyMist()
        {
            var source = EdgeMistLook.Settings;
            if (source == null)
            {
                return;
            }

            if (ReferenceEquals(source, _mist))
            {
                return;
            }

            // 共通の設定が入れ替わったとき（ステージが変わった等）と、1秒ごとに写し直す。形や濃さは共通の設定に合わせる。
            if (_mist == null || !ReferenceEquals(source, _mistSource))
            {
                _mistSource = source;
                _mist = new EdgeMistSettings();
                _mistDirty = true;
            }

            if (_mistDirty)
            {
                _mistDirty = false;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(_mistSource), _mist);
            }

            var s = _settings;
            _mist.mistColor = s.mistColor;
            _mist.shadeTint = s.mistShadeTint;
            _mist.edgeBandColor = s.mistEdgeBandColor;
            _mist.edgeBandStrength = s.mistEdgeBandStrength;
            _mist.enemyRimColor = s.mistEnemyRimColor;
            EdgeMistLook.Settings = _mist;
        }

        // 床の役割の物のうち、一番大きい物を「床の外」、ほかを「丸い床」とみなして色を入れる。
        private void RecolorFloor()
        {
            _floorDone = true;
            var floors = new List<(StageProp Prop, float Size)>();
            foreach (var prop in StageProp.All)
            {
                if (prop != null && prop.role == StagePropRole.Floor)
                {
                    var bounds = prop.VisualBounds;
                    floors.Add((prop, bounds.size.x * bounds.size.y));
                }
            }

            if (floors.Count == 0)
            {
                Debug.LogWarning("[CyberCleanLook] 床の役割の背景が見つからないので、床の色は変えません。");
                return;
            }

            var outer = floors.Count > 1 ? floors[0] : default;
            foreach (var floor in floors)
            {
                if (floors.Count > 1 && floor.Size > outer.Size)
                {
                    outer = floor;
                }
            }

            var log = new StringBuilder("[CyberCleanLook] 床の色を変えました:");
            foreach (var floor in floors)
            {
                var isOuter = floors.Count > 1 && floor.Prop == outer.Prop;
                var root = floor.Prop.visualRoot != null ? floor.Prop.visualRoot : floor.Prop.transform;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Recolor(renderer, isOuter, log);
                }
            }

            ApplyFloorColors();
            Debug.Log(log.ToString());
        }

        // 床の複製の材質に色を入れる（Play 中に設定を変えたときも、1秒ごとに入れ直す）。
        private void ApplyFloorColors()
        {
            var floor = _settings.floorColor;
            var outer = _settings.outerTint;
            foreach (var (copy, source, isOuter, multiply) in _copies)
            {
                if (copy == null)
                {
                    continue;
                }

                var color = isOuter ? outer : floor;
                copy.SetColor("_BaseColor", multiply ? source * color : color);
            }
        }

        private void Recolor(Renderer renderer, bool isOuter, StringBuilder log)
        {
            var original = renderer.sharedMaterials;
            var copies = new Material[original.Length];
            for (var i = 0; i < original.Length; i++)
            {
                var source = original[i];
                if (source == null || !source.HasProperty("_BaseColor"))
                {
                    copies[i] = source;
                    continue;
                }

                var copy = new Material(source) { name = source.name + " (CyberClean)", hideFlags = HideFlags.DontSave };
                // 模様の絵がある材質や「床の外」は今の色に掛け、絵の無い丸い床は色そのものを入れ替える。
                var hasTexture = source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null;
                var multiply = isOuter || hasTexture;
                copies[i] = copy;
                _copies.Add((copy, source.GetColor("_BaseColor"), isOuter, multiply));
                log.Append($"\n  {renderer.name} / {source.name}（{(multiply ? "色を掛けた" : "色を入れ替えた")}）");
            }

            _recolored.Add((renderer, original));
            renderer.sharedMaterials = copies;
        }

        private void RestoreFloor()
        {
            foreach (var (renderer, original) in _recolored)
            {
                if (renderer != null)
                {
                    renderer.sharedMaterials = original;
                }
            }

            _recolored.Clear();
            foreach (var (copy, _, _, _) in _copies)
            {
                if (copy != null)
                {
                    Destroy(copy);
                }
            }

            _copies.Clear();
            _floorDone = false;
        }
    }
}
