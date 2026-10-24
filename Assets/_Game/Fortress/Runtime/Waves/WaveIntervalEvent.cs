using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ウェーブの合間に行うイベント（歯みがきタイムなど）の土台。データ（このアセット）＋動かす係（<see cref="WaveIntervalRunner"/>）の組。
    /// 新しいイベントは、これと係を継承して作れば、ウェーブの進め方のコードを触らずに足せる。
    /// 係は合間が始まると全員のPCで作られ、終わると消える。結果を決める（コアを回復するなど）のは Host（オフラインでは自分）だけ。
    /// </summary>
    public abstract class WaveIntervalEvent : ScriptableObject
    {
        [Tooltip("要塞デザイナーに出す名前。")]
        public string displayName = "";

        /// <summary>イベント全体の長さ（秒。結果を見せる時間も含む）。合間はこの長さで終わる（係が早く終われば早く終わる）。</summary>
        public abstract float DurationSeconds { get; }

        /// <summary>この合間の係を host に付けて返す。</summary>
        public abstract WaveIntervalRunner CreateRunner(GameObject host);

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    }

    /// <summary>合間の始まりに係へ渡す情報。</summary>
    public readonly struct WaveBreakContext
    {
        public WaveBreakContext(int clearedWaveIndex, bool hasAuthority, int connectedPlayerMask, int localPlayer)
        {
            ClearedWaveIndex = clearedWaveIndex;
            HasAuthority = hasAuthority;
            ConnectedPlayerMask = connectedPlayerMask;
            LocalPlayer = localPlayer;
        }

        /// <summary>今クリアしたウェーブの番号（0 から）。</summary>
        public int ClearedWaveIndex { get; }

        /// <summary>結果を決めるPC（オフライン／Host）か。</summary>
        public bool HasAuthority { get; }

        /// <summary>試合にいるプレイヤー（bit p = P(p+1)）。オフラインでは全員。</summary>
        public int ConnectedPlayerMask { get; }

        /// <summary>このPCのプレイヤー（0〜3）。</summary>
        public int LocalPlayer { get; }

        public int ConnectedCount
        {
            get
            {
                var count = 0;
                for (var p = 0; p < 4; p++)
                {
                    if ((ConnectedPlayerMask & (1 << p)) != 0)
                    {
                        count++;
                    }
                }

                return Mathf.Max(1, count);
            }
        }
    }

    /// <summary>
    /// 合間のイベントを動かす係。全員のPCで動く。
    /// ・Host は <see cref="State"/> に今の様子（進み具合など）を書く → 小さな数の並びとして全員へ届く（Client は <see cref="ApplyState"/> で受け取る）
    /// ・各PCのプレイヤーの入力（握った回数など）は <see cref="FortressWaves.SetLocalBreakInput"/> で数えれば、Host に届いて
    ///   <see cref="FortressWaves.BreakInput"/> で読める
    /// </summary>
    public abstract class WaveIntervalRunner : MonoBehaviour
    {
        /// <summary>全員へ届ける様子の数の最大個数。</summary>
        public const int MaxStateValues = 12;

        /// <summary>今の様子（Host が書く。Client は届いた値）。</summary>
        public readonly float[] State = new float[MaxStateValues];

        public WaveIntervalEvent Definition { get; internal set; }

        public WaveBreakContext Context { get; private set; }

        /// <summary>合間が始まってからの秒数。</summary>
        public float Elapsed { get; private set; }

        /// <summary>Host: 早く終わってよいとき true（なくても <see cref="WaveIntervalEvent.DurationSeconds"/> で終わる）。</summary>
        public virtual bool IsFinished => false;

        internal void BeginInternal(WaveBreakContext context)
        {
            Context = context;
            Elapsed = 0f;
            System.Array.Clear(State, 0, State.Length);
            OnBegin();
        }

        internal void TickInternal(float dt)
        {
            Elapsed += dt;
            OnTick(dt);
        }

        internal void EndInternal()
        {
            OnEnd();
        }

        /// <summary>Client: Host から様子が届いた。</summary>
        internal void ApplyState(float[] values, int count)
        {
            System.Array.Copy(values, State, Mathf.Min(count, State.Length));
            OnStateReceived();
        }

        protected abstract void OnBegin();

        protected abstract void OnTick(float dt);

        protected abstract void OnEnd();

        protected virtual void OnStateReceived()
        {
        }
    }
}
