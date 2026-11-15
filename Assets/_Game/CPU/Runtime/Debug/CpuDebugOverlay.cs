using MS2026.Fortress;
using MS2026.Fortress.Hud;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU の「頭の中」の表示（開発用）。デバッグ表示を出してよいとき（エディタの中・起動オプション -debug-ui）だけ。
    /// F10 を押すたびに: 出さない → 表（右下） → 表 ＋ 線（見えている範囲・向きごとの敵の多さ・砲身の向き）。
    /// ゲームの画面の決まり（CPU と人を区別しない）に関わらない開発用なので、最初は出さない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class CpuDebugOverlay : MonoBehaviour
    {
        public const Key ToggleKey = Key.F10;

        private const float PanelWidth = 470f;
        private const float RowHeight = 20f;

        private static readonly string[] ModeNames = { "出さない", "表", "表と線" };

        private CpuDirector _director;
        private Material _lineMaterial;
        private GUIStyle _style;
        private GUIStyle _boxStyle;

        /// <summary>0 = 出さない、1 = 表、2 = 表と線。</summary>
        public int Mode { get; set; }

        public void Bind(CpuDirector director)
        {
            _director = director;
        }

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
            {
                Destroy(_lineMaterial);
            }
        }

        private void Update()
        {
            if (!DebugUi.Visible)
            {
                Mode = 0;
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[ToggleKey].wasPressedThisFrame)
            {
                Mode = (Mode + 1) % ModeNames.Length;
                Debug.Log($"[CPU] 頭の中の表示: {ModeNames[Mode]}（{ToggleKey} で切り替え）");
            }
        }

        // ───────── 表 ─────────

        private void OnGUI()
        {
            if (Mode == 0 || _director == null)
            {
                return;
            }

            EnsureStyles();
            var rows = 2;
            for (var p = 0; p < CpuSeatRules.PlayerCount; p++)
            {
                rows += _director.GetPlayer(p) != null ? 1 : 0;
            }

            // 左上（HUD の右上の体力・右下のスキルのゲージ・左の仲間の列と重ならない所）。
            var height = rows * RowHeight + 12f;
            var rect = new Rect(10f, 10f, PanelWidth, height);
            GUI.Box(rect, GUIContent.none, _boxStyle);

            var line = new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, RowHeight);
            GUI.Label(line, Header(), _style);
            line.y += RowHeight;

            var any = false;
            for (var p = 0; p < CpuSeatRules.PlayerCount; p++)
            {
                var cpu = _director.GetPlayer(p);
                if (cpu == null)
                {
                    continue;
                }

                any = true;
                GUI.color = Color.Lerp(FortressColors.PlayerColor(p), Color.white, 0.35f);
                GUI.Label(line, Row(cpu), _style);
                GUI.color = Color.white;
                line.y += RowHeight;
            }

            if (!any)
            {
                GUI.Label(line, "CPU の席はありません", _style);
            }
        }

        private string Header()
        {
            var text = $"CPU（{ToggleKey}: {ModeNames[Mode]}）{CpuLevels.Label(_director.Level)}";
            var recorder = _director.Recorder;
            if (recorder != null && recorder.InMatch)
            {
                text += $"・試合 {recorder.MatchNumber}" + (FortressWaves.IsActive ? $"・W{FortressWaves.WaveNumber}" : string.Empty);
            }

            var autoplay = _director.Autoplay;
            if (autoplay.Active)
            {
                text += $"・自動 {autoplay.DoneMatches}/{autoplay.TotalMatches}・{autoplay.TimeScale:0.#}倍";
                if (autoplay.WaitingSeconds > 0f)
                {
                    text += $"（次まで {autoplay.WaitingSeconds:0} 秒）";
                }
            }

            return text;
        }

        // 1行に収まるよう、大事な数を前に、行動の点は「今の行動」と「次に高い行動」だけ（全部の点は結果の表・Console のメニューで見る）。
        private static string Row(CpuPlayer cpu)
        {
            var brain = cpu.Brain;
            var current = brain.Current;
            var currentIndex = -1;
            var nextIndex = -1;
            for (var i = 0; i < brain.Behaviors.Count; i++)
            {
                if (ReferenceEquals(brain.Behaviors[i], current))
                {
                    currentIndex = i;
                }
                else if (brain.Scores[i] > 0f && (nextIndex < 0 || brain.Scores[i] > brain.Scores[nextIndex]))
                {
                    nextIndex = i;
                }
            }

            var info = new CpuTurretInfo(cpu.Context.Turret);
            var sight = cpu.Eyes.Perceived;
            var now = currentIndex >= 0 ? $"{Label(current.Id)} {brain.Scores[currentIndex]:0.00}" : "なし";
            var next = nextIndex >= 0 ? $"  次 {Label(brain.Behaviors[nextIndex].Id)} {brain.Scores[nextIndex]:0.00}" : string.Empty;
            return $"P{cpu.Player + 1} {now}｜握 {cpu.Hands.Grip01:0.00} 熱 {(info.Valid ? info.Heat01 * 100f : 0f):0}%" +
                   $" 敵 {sight.Total:0}{(sight.HasView ? string.Empty : "?")} 上手さ {AverageScale(cpu):0.0}" +
                   (cpu.CanMove ? $" 移動 {cpu.MoveIntent.magnitude:0.0}" : string.Empty) + next;
        }

        // 行動の名前（日本語。知らない行動は Id のまま）。
        private static string Label(string id)
        {
            switch (id)
            {
                case CpuShootBehavior.BehaviorId: return "撃つ";
                case CpuCoolDownBehavior.BehaviorId: return "冷ます";
                case CpuSkillBehavior.BehaviorId: return "スキル";
                case CpuSmashBallBehavior.BehaviorId: return "ボール";
                case CpuBrushingBehavior.BehaviorId: return "歯みがき";
                case "revive": return "救助";
                case "struggle": return "抜け出す";
                default: return id;
            }
        }

        // 分野ごとの上手さの目盛りの平均（0 = 初めて遊ぶ人 … 3 = とても上手い人）。
        private static float AverageScale(CpuPlayer cpu)
        {
            var sum = 0f;
            var count = 0;
            foreach (var area in CpuPlugins.SkillAreas)
            {
                sum += cpu.Context.ScaleOf(area.Id);
                count++;
            }

            return count > 0 ? sum / count : CpuLevels.Scale(cpu.Level, 0f);
        }

        private void EnsureStyles()
        {
            if (_style != null)
            {
                return;
            }

            _style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = false, clipping = TextClipping.Clip };
            _style.normal.textColor = Color.white;
            var background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            background.Apply();
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = background;
        }

        // ───────── 線 ─────────

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (Mode < 2 || _director == null || camera == null || camera.cameraType == CameraType.Preview ||
                camera.cameraType == CameraType.Reflection || !EnsureLineMaterial())
            {
                return;
            }

            GL.PushMatrix();
            _lineMaterial.SetPass(0);
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;
            GL.Begin(GL.LINES);
            for (var p = 0; p < CpuSeatRules.PlayerCount; p++)
            {
                var cpu = _director.GetPlayer(p);
                if (cpu != null && cpu.Context.Turret != null)
                {
                    DrawCpu(cpu, FortressColors.PlayerColor(p));
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        private static void DrawCpu(CpuPlayer cpu, Color color)
        {
            var turret = cpu.Context.Turret;
            var origin = turret.MuzzlePosition;
            var z = origin.z - 0.05f;
            var sight = cpu.Eyes.Perceived;

            // 見えている範囲（画面に映る四角）。
            if (sight.HasView)
            {
                var view = sight.View;
                var right = view.Right * view.HalfSize.x;
                var up = view.Up * view.HalfSize.y;
                var c = view.Center;
                GL.Color(new Color(color.r, color.g, color.b, 0.6f));
                Line(c - right - up, c + right - up, z);
                Line(c + right - up, c + right + up, z);
                Line(c + right + up, c - right + up, z);
                Line(c - right + up, c - right - up, z);
            }

            // 向きごとの敵の多さ（長いほど多い。ビームが壁で止まる所まで）。
            var weights = sight.Weights;
            var max = 1f;
            foreach (var w in weights)
            {
                max = Mathf.Max(max, w);
            }

            for (var s = 0; s < weights.Length; s++)
            {
                if (weights[s] <= 0f)
                {
                    continue;
                }

                var angle = CpuSectorMath.CenterOf(s, weights.Length) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var length = Mathf.Min(sight.ObstacleDistances[s], 1f + (sight.ObstacleDistances[s] - 1f) * weights[s] / max);
                GL.Color(new Color(color.r, color.g, color.b, 0.35f + 0.6f * weights[s] / max));
                Line(origin, (Vector2)origin + direction * length, z);
            }

            // 砲身の向き（握っている強さで明るく）。
            GL.Color(Color.Lerp(new Color(1f, 1f, 1f, 0.4f), Color.white, cpu.Hands.Grip01));
            Line(origin, (Vector2)origin + (Vector2)turret.AimDirection * 3f, z);
        }

        private static void Line(Vector2 a, Vector2 b, float z)
        {
            GL.Vertex3(a.x, a.y, z);
            GL.Vertex3(b.x, b.y, z);
        }

        private bool EnsureLineMaterial()
        {
            if (_lineMaterial != null)
            {
                return true;
            }

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                return false;
            }

            _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
            _lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            return true;
        }
    }
}
