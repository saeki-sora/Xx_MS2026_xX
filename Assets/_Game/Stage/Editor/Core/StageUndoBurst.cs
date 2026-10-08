using UnityEditor;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// ホイールを回す・キーを押し続ける・欄の名前をドラッグする、のような「細かい変更の連続」を、1回の「元に戻す」にまとめる。
    /// 同じ名前の変更が <see cref="Window"/> 秒以内に続く間は同じまとまりにする。
    /// </summary>
    public static class StageUndoBurst
    {
        private const double Window = 0.6;

        private static string _label;
        private static double _last;
        private static int _group = -1;

        /// <summary>変更の前に呼ぶ。</summary>
        public static void Before(string label)
        {
            var now = EditorApplication.timeSinceStartup;
            if (label != _label || now - _last > Window || _group < 0)
            {
                Undo.IncrementCurrentGroup();
                _group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(label);
            }

            _label = label;
            _last = now;
        }

        /// <summary>変更の後に呼ぶ。</summary>
        public static void After()
        {
            if (_group >= 0)
            {
                Undo.CollapseUndoOperations(_group);
            }

            StagePropTransformOps.Commit();
            StageGameCamera.MarkDirty();
        }
    }
}
