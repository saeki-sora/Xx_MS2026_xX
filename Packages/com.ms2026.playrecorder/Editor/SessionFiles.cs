using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>1 回の録画の情報（meta.json）。</summary>
    [Serializable]
    internal sealed class SessionMeta
    {
        public int version = 1;
        public string createdAt;      // yyyy-MM-dd_HH-mm-ss（出力ファイル名に使う）
        public string projectName;
        public string sceneName;
        public int sourceWidth;
        public int sourceHeight;
        public int audioSampleRate;
        public int audioChannels;     // 0 = 音なし
        public double duration;       // 秒（一時停止中は数えない）
        public bool complete;         // 正常に止められたか（false ならクラッシュ等からの救出）
        public int capturedFrames;
        public int droppedFrames;
    }

    /// <summary>取り込んだ 1 枚の場所。</summary>
    internal struct FrameEntry
    {
        public long Offset;
        public int Length;
        public double Time;
        public int Width;
        public int Height;

        public const int ByteSize = 8 + 4 + 8 + 4 + 4;
    }

    /// <summary>
    /// 一時ファイルの置き場所と読み書き。Library/PlayRecorder/Sessions/&lt;日時&gt;/ に
    /// frames.bin（JPEG を連結）・frames.idx（各 JPEG の位置と時刻）・audio.raw（float の生音）・meta.json を置く。
    /// 書き出しに成功したらフォルダごと消す。Unity が落ちても次に開いたとき書き出しを再開できる。
    /// </summary>
    internal static class SessionFiles
    {
        public const string FramesFile = "frames.bin";
        public const string IndexFile = "frames.idx";
        public const string AudioFile = "audio.raw";
        public const string MetaFile = "meta.json";
        public const string FailedFile = "failed.txt";

        public static string Root => Path.Combine(PlayRecorderSettings.ProjectRoot, "Library", "PlayRecorder", "Sessions");

        public static string CreateSessionDir(out string createdAt)
        {
            createdAt = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string dir = Path.Combine(Root, createdAt);
            int n = 2;
            while (Directory.Exists(dir)) dir = Path.Combine(Root, createdAt + "_" + n++);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void WriteMeta(string dir, SessionMeta meta) =>
            File.WriteAllText(Path.Combine(dir, MetaFile), JsonUtility.ToJson(meta, true));

        public static SessionMeta ReadMeta(string dir)
        {
            string p = Path.Combine(dir, MetaFile);
            if (!File.Exists(p)) return null;
            try { return JsonUtility.FromJson<SessionMeta>(File.ReadAllText(p)); }
            catch { return null; }
        }

        /// <summary>書き出し待ちのフォルダ（古い順）。失敗印の付いたものと録画中のものは除く。</summary>
        public static List<string> PendingSessions(string exclude)
        {
            var list = new List<string>();
            if (!Directory.Exists(Root)) return list;
            foreach (var d in Directory.GetDirectories(Root))
            {
                if (exclude != null && PathEquals(d, exclude)) continue;
                if (File.Exists(Path.Combine(d, FailedFile))) continue;
                if (!File.Exists(Path.Combine(d, MetaFile))) continue;
                list.Add(d);
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        public static void MarkFailed(string dir, string reason)
        {
            try { File.WriteAllText(Path.Combine(dir, FailedFile), reason ?? ""); }
            catch { /* 書けなくても致命的ではない */ }
        }

        public static void DeleteQuietly(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (Exception e) { Debug.LogWarning($"[録画プレイ] 一時フォルダを消せませんでした: {dir}\n{e.Message}"); }
        }

        /// <summary>frames.idx を読む。途中で途切れていても読めたところまで返す（クラッシュ救出用）。</summary>
        public static List<FrameEntry> ReadIndex(string dir)
        {
            var list = new List<FrameEntry>();
            string idx = Path.Combine(dir, IndexFile);
            string bin = Path.Combine(dir, FramesFile);
            if (!File.Exists(idx) || !File.Exists(bin)) return list;
            long binLength = new FileInfo(bin).Length;
            using (var fs = new FileStream(idx, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var br = new BinaryReader(fs))
            {
                while (fs.Length - fs.Position >= FrameEntry.ByteSize)
                {
                    var e = new FrameEntry
                    {
                        Offset = br.ReadInt64(),
                        Length = br.ReadInt32(),
                        Time = br.ReadDouble(),
                        Width = br.ReadInt32(),
                        Height = br.ReadInt32(),
                    };
                    if (e.Offset < 0 || e.Length <= 0 || e.Offset + e.Length > binLength) break;
                    list.Add(e);
                }
            }
            return list;
        }

        static bool PathEquals(string a, string b) =>
            string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}
