namespace MS2026.GripInputBridge.Relay
{
    /// <summary>
    /// 放送する4人分の値を作る（純粋・テストあり。<see cref="GripRelaySession"/> が放送のスレッドから呼ぶ）。
    /// <list type="bullet">
    /// <item>このPCに Arduino がある: 値が届いているセンサーは「Arduino とキーボード（Q/W/O/P）の大きい方」、届いていないセンサーは「値なし」。</item>
    /// <item>Arduino が無く、テスト用の放送（-grip-relay-send）: 4人分ともキーボードの値。</item>
    /// <item>Arduino が無く、テスト用でもない: 放送しない（false）。放送が2か所から来て、本物が使われなくなるのを防ぐ。</item>
    /// </list>
    /// </summary>
    public static class GripRelaySampleBuilder
    {
        /// <param name="packet">値を入れる先（4人分と ForcedTestSender を書く）。</param>
        /// <param name="device">このPCの Arduino（無ければ null）。どのスレッドから読んでも安全な物。</param>
        /// <param name="keyboard">キーボードの4人分の値（0-1）。</param>
        /// <param name="forceSend">Arduino が無くてもキーボードを放送する（テスト用）。</param>
        /// <returns>放送するなら true。</returns>
        public static bool Fill(GripRelayPacket packet, IGripRelayDevice device, float[] keyboard, bool forceSend)
        {
            if (device == null && !forceSend)
            {
                return false;
            }

            packet.ForcedTestSender = device == null;
            for (var i = 0; i < GripRelayPacket.SensorCount; i++)
            {
                var key = keyboard != null && i < keyboard.Length ? keyboard[i] : 0f;
                ref var sensor = ref packet.Sensors[i];
                if (device == null)
                {
                    sensor.Raw = key;
                    sensor.Received = key;
                    sensor.Connected = true;
                }
                else if (device.IsConnected(i))
                {
                    // 実機を挿したままでも、キーボード（Q/W/O/P）で試せるよう大きい方を送る（会場にはキーボードが無い）。
                    var value = device.GetRawValue(i);
                    sensor.Raw = key > value ? key : value;
                    sensor.Received = device.GetReceivedValue(i);
                    sensor.Connected = true;
                }
                else
                {
                    // このセンサーの値は Arduino から届いていない。受け取る側はキーボードだけで動く。
                    sensor.Raw = 0f;
                    sensor.Received = 0f;
                    sensor.Connected = false;
                }
            }

            return true;
        }
    }
}
