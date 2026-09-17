using System;

namespace MS2026.GripInputBridge.Processing
{
    /// <summary>
    /// ヒステリシス付きのON/OFF判定。設計書 7.2 参照。
    /// onThresholdを超えたら「握った」、offThreshold未満で「離した」とみなす。
    /// ON判定としきい値OFF判定に"のりしろ"を持たせることで、境界付近の値のガタつきによる
    /// チャタリング（連続反転）を防ぐ。onThreshold &gt; offThreshold が前提。
    /// </summary>
    public sealed class GripEdgeDetector
    {
        private readonly float _onThreshold;
        private readonly float _offThreshold;

        public GripEdgeDetector(float onThreshold, float offThreshold)
        {
            if (onThreshold < offThreshold)
            {
                throw new ArgumentException(
                    $"onThreshold({onThreshold})はoffThreshold({offThreshold})以上である必要があります。");
            }

            _onThreshold = onThreshold;
            _offThreshold = offThreshold;
        }

        /// <summary>現在「握っている」と判定されているか。</summary>
        public bool IsGripping { get; private set; }

        /// <summary>
        /// 新しい正規化値(0-1)を渡して状態を更新する。
        /// 握り始め/離しのどちらかが起きた場合は true を返す(起きなかった場合は false)。
        /// </summary>
        public bool Update(float normalizedValue)
        {
            if (!IsGripping && normalizedValue >= _onThreshold)
            {
                IsGripping = true;
                return true;
            }

            if (IsGripping && normalizedValue <= _offThreshold)
            {
                IsGripping = false;
                return true;
            }

            return false;
        }

        /// <summary>内部状態をリセットする(トランスポート切替時などに使用)。</summary>
        public void Reset()
        {
            IsGripping = false;
        }
    }
}
