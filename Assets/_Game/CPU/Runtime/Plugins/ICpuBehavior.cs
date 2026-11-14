namespace MS2026.Cpu
{
    /// <summary>
    /// CPU の「やりたいこと」1つ（撃つ・冷ます・スキル・救助など）。頭（<see cref="CpuBrain"/>）が全部の点を比べて、一番高いものをやる。
    /// 新しい行動は、これを書いて <see cref="CpuPlugins.RegisterBehavior"/> で登録するだけで増える（頭やゲームの側は直さない）。
    /// CPU 1人ごとに1つずつ作られる（登録するのは作り方）ので、中に状態を持ってよい。
    /// </summary>
    public interface ICpuBehavior
    {
        /// <summary>見分けるための名前（英数字。頭の中の表示・記録に出る）。</summary>
        string Id { get; }

        /// <summary>今これをやる価値（0 以下 = やらない）。考え直すたびに呼ばれる。手は動かさない。</summary>
        float Score(CpuContext context);

        /// <summary>この行動に切り替わったとき。</summary>
        void Enter(CpuContext context);

        /// <summary>この行動をしている間、毎フレーム。手（<see cref="CpuContext.Hands"/>）を動かす。</summary>
        void Tick(CpuContext context, float deltaTime);

        /// <summary>ほかの行動に切り替わる・CPU が止まるとき。手は頭が離すので、ここで離さなくてよい。</summary>
        void Exit(CpuContext context);
    }
}
