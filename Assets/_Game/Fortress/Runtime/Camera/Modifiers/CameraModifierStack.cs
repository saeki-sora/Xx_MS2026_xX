using System.Collections.Generic;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 一時演出を積み重ねて順に適用する。終わった演出は自動で取り除く。
    /// 連打で無限に積み上がらないよう、上限を超えたら古い物から捨てる。
    /// </summary>
    public sealed class CameraModifierStack
    {
        private readonly List<ICameraViewModifier> _modifiers = new List<ICameraViewModifier>();
        private readonly int _capacity;

        public CameraModifierStack(int capacity = 16)
        {
            _capacity = capacity < 1 ? 1 : capacity;
        }

        public int Count => _modifiers.Count;

        public void Add(ICameraViewModifier modifier)
        {
            if (modifier == null)
            {
                return;
            }

            if (_modifiers.Count >= _capacity)
            {
                _modifiers.RemoveAt(0);
            }

            _modifiers.Add(modifier);
        }

        public void Clear() => _modifiers.Clear();

        public CameraViewSettings Apply(CameraViewSettings view, float deltaTime)
        {
            for (var i = 0; i < _modifiers.Count; i++)
            {
                if (!_modifiers[i].Apply(ref view, deltaTime))
                {
                    _modifiers.RemoveAt(i);
                    i--;
                }
            }

            return view;
        }
    }
}
