namespace MS2026.Fortress
{
    /// <summary>
    /// <see cref="LaserTurret"/>が読む握力の入力元。未設定ならGripInputBridgeを直接読む(オフライン)。
    /// ネット対戦のHostでは、TurretNetworkHubが「自分の分はローカルのセンサー、他人の分はClientから届いた値」を返す。
    /// </summary>
    public interface ITurretGripSource
    {
        void GetGrip(int playerIndex, out bool isGripping, out float grip01);
    }
}
