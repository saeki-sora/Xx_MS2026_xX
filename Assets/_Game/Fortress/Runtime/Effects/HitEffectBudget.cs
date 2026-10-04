using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 「1秒に最大N個まで」を守るための残り枠（トークンバケツ）。数千体の敵に同時に当たっても
    /// エフェクトが出すぎないようにする。枠は1秒分（N個）まで貯まり、1秒にN個ずつ回復する。
    /// Unity APIに依存しない純粋なクラス（テスト: HitEffectBudgetTests）。
    /// </summary>
    public sealed class HitEffectBudget
    {
        private readonly int _hardCapPerFrame;
        private float _tokens;
        private float _perSecond;

        /// <param name="hardCapPerFrame">上限を決めていない（0）ときも含め、1フレームに出してよい絶対の上限。</param>
        public HitEffectBudget(int hardCapPerFrame)
        {
            _hardCapPerFrame = Mathf.Max(0, hardCapPerFrame);
        }

        /// <summary>1秒あたりの上限。0以下なら無制限（1フレームの絶対上限だけが効く）。</summary>
        public float PerSecond
        {
            get => _perSecond;
            set
            {
                if (Mathf.Approximately(value, _perSecond))
                {
                    return;
                }

                // 上限を変えた直後は、新しい上限いっぱいから始める。
                _perSecond = value;
                _tokens = Capacity;
            }
        }

        private float Capacity => Mathf.Max(1f, _perSecond);

        /// <summary>今のフレームで出してよい数。</summary>
        public int Available => _perSecond <= 0f
            ? _hardCapPerFrame
            : Mathf.Min(_hardCapPerFrame, Mathf.FloorToInt(_tokens));

        /// <summary>経過時間ぶん枠を回復する。</summary>
        public void Refill(float deltaTime)
        {
            if (_perSecond <= 0f || deltaTime <= 0f)
            {
                return;
            }

            _tokens = Mathf.Min(Capacity, _tokens + _perSecond * deltaTime);
        }

        /// <summary>実際に出した数だけ枠を減らす。</summary>
        public void Consume(int count)
        {
            if (_perSecond <= 0f || count <= 0)
            {
                return;
            }

            _tokens = Mathf.Max(0f, _tokens - count);
        }
    }
}
