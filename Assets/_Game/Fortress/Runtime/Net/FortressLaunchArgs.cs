using System;
using System.Globalization;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 検証用のコマンドライン引数。1台のPCでexeを複数起動して接続確認するためのもの(UI操作を省略できる)。
    /// D-Driveの -ddrive-net host/client は ConnectionData(PlayerIndex)を載せずに接続してしまい承認で弾かれるため、
    /// 自動接続はこちらの引数で行い、D-Drive側は Default Net Start = Manual のまま使う。
    ///
    ///   -fortress-start host|client   起動直後にHost開始/Client接続する(未指定なら接続UIを出すだけ)
    ///   -fortress-player 0-3          自分のプレイヤー番号(接続UIの初期値にもなる)
    ///   -fortress-address 192.168.x.x Clientの接続先(未指定なら接続UIの既定値)
    ///   -fortress-tile                ウィンドウモード時、プレイヤー番号に応じて画面を2x2に分割した位置へ移動する
    /// </summary>
    public struct FortressLaunchArgs
    {
        public const string StartFlag = "-fortress-start";
        public const string PlayerFlag = "-fortress-player";
        public const string AddressFlag = "-fortress-address";
        public const string TileFlag = "-fortress-tile";

        public enum StartMode
        {
            None,
            Host,
            Client
        }

        public StartMode Start;
        public int? PlayerIndex;
        public string Address;
        public bool Tile;

        public static FortressLaunchArgs Parse(string[] args)
        {
            var result = new FortressLaunchArgs();
            if (args == null)
            {
                return result;
            }

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case StartFlag:
                        var mode = NextValue(args, ref i);
                        if (string.Equals(mode, "host", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Start = StartMode.Host;
                        }
                        else if (string.Equals(mode, "client", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Start = StartMode.Client;
                        }

                        break;

                    case PlayerFlag:
                        if (int.TryParse(NextValue(args, ref i), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index <= 3)
                        {
                            result.PlayerIndex = index;
                        }

                        break;

                    case AddressFlag:
                        result.Address = NextValue(args, ref i);
                        break;

                    case TileFlag:
                        result.Tile = true;
                        break;
                }
            }

            return result;
        }

        private static string NextValue(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
            {
                return null;
            }

            i++;
            return args[i];
        }
    }
}
