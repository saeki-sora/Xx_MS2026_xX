namespace MS2026.Fortress
{
    /// <summary>
    /// 効果音をどのPCで鳴らすか（サウンドスタジオ、2026-11-22）。会場では4台のPCが並ぶので、
    /// 「その人の事」の音（自分のレーザーなど）を全員のPCで鳴らすと、同じ音が4台から重なって聞こえる。
    /// </summary>
    public enum SoundAudience
    {
        /// <summary>おまかせ: 誰の事かが分かる（持ち主の番号が渡された）ときはその人のPCだけ、分からないときは全員のPC。</summary>
        Auto = 0,

        /// <summary>全員のPCで鳴らす。</summary>
        Everyone = 1,

        /// <summary>その人のPCだけで鳴らす（CPU の事は Host のPCで鳴らす）。</summary>
        OwnerOnly = 2,
    }
}
