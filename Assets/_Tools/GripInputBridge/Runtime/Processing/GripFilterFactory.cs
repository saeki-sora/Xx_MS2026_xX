using System;
using MS2026.GripInputBridge.Data;

namespace MS2026.GripInputBridge.Processing
{
    /// <summary>
    /// <see cref="GripFilterSettings"/> の内容に応じて、プレイヤー1人分の
    /// <see cref="IGripSignalFilter"/> インスタンスを生成する。
    /// フィルタは内部状態を持つため、呼び出すたびに新しいインスタンスを返す
    /// （プレイヤー間・トランスポート切替間で状態を共有しないようにするため）。
    /// </summary>
    public static class GripFilterFactory
    {
        public static IGripSignalFilter Create(GripFilterSettings settings)
        {
            if (settings == null)
            {
                return new PassthroughFilter();
            }

            return settings.filterType switch
            {
                GripFilterType.MovingAverage => new MovingAverageFilter(settings.windowSize),
                GripFilterType.ExponentialMovingAverage => new ExponentialMovingAverageFilter(settings.emaAlpha),
                GripFilterType.Median => new MedianFilter(settings.windowSize),
                GripFilterType.MedianThenExponentialMovingAverage => new MedianThenEmaFilter(settings.windowSize, settings.emaAlpha),
                _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.filterType, "未対応のGripFilterTypeです。")
            };
        }
    }
}
