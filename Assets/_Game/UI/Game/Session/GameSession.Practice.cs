using System;
using System.Collections;
using MS2026.HowToPlay;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.UI.Game
{
    // あそびかた（2026-10-29、ユーザー決定: 「握って練習」＋「読み込み中のヒント」。設定は Assets/HowToPlay/Resources/HowToPlaySettings）。
    // ・ホスト: ステージ抽選の後、段階を Practice にする。全員が3つの課題を終えたら（または時間切れで）少し待ち、幕で隠してゲームへ進む。
    // ・全員: Practice の間は自分の握力（このPCのセンサー＝番号0）で練習を動かし、参加側はホストへ送る。ホストは全員分をまとめて配る。
    //   画面と音は各PCが自分で出す（PracticeView）。
    // ・ゲームへの幕が覆いきったら、歯みがきタイムのヒント（LoadingTipView）を出し、最低秒数たつまで幕を上げない。
    public sealed partial class GameSession
    {
        private const float PracticeSendInterval = 1f / 15f;

        private readonly PracticeBoard _practice = new PracticeBoard();
        private readonly PracticeTrainer _practiceTrainer = new PracticeTrainer();
        private Coroutine _practiceWait;
        private bool _practiceRunning;
        private float _practiceNextSend;
        private int _practiceSentVersion = -1;
        private PracticeBoard.Seat _practiceSentInput;

        private static HowToPlaySettings HowToPlay => HowToPlaySettings.Load();

        // ───────── ホスト: 抽選 → 練習 → ゲーム ─────────

        /// <summary>ホスト: 練習を始める（練習を使わない設定なら false。すぐゲームへ進める）。</summary>
        private bool TryBeginPractice()
        {
            var settings = HowToPlay;
            if (!settings.practiceEnabled || Roster.phase == LobbyPhase.Practice)
            {
                return false;
            }

            ChangePhase(LobbyPhase.Practice);
            CancelPractice();
            _practiceWait = StartCoroutine(WaitForPractice(settings));
            return true;
        }

        private IEnumerator WaitForPractice(HowToPlaySettings settings)
        {
            var finishedAt = -1f;
            while (IsHost && Roster.phase == LobbyPhase.Practice)
            {
                if (!_practice.finished && (_practice.AllDone || _practice.TimeUp))
                {
                    _practice.Finish();
                }

                if (_practice.finished)
                {
                    if (finishedAt < 0f)
                    {
                        finishedAt = Time.unscaledTime;
                    }
                    else if (Time.unscaledTime - finishedAt >= settings.finishDelaySeconds)
                    {
                        break;
                    }
                }

                yield return null;
            }

            _practiceWait = null;
            if (IsHost && Roster.phase == LobbyPhase.Practice)
            {
                ProceedToLoadGame();
            }
        }

        private void CancelPractice()
        {
            if (_practiceWait != null)
            {
                StopCoroutine(_practiceWait);
                _practiceWait = null;
            }
        }

        // ───────── 全員: 段階の変化と毎フレーム ─────────

        private void OnPracticePhase(LobbyPhase before, LobbyPhase after)
        {
            if (after == LobbyPhase.Practice && before != LobbyPhase.Practice)
            {
                var settings = HowToPlay;
                _practice.Reset(settings.timeLimitSeconds);
                _practiceTrainer.Reset();
                _practiceSentVersion = -1;
                _practiceSentInput = new PracticeBoard.Seat { grip = byte.MaxValue };
                _practiceNextSend = 0f;
                _practiceRunning = true;
                SyncPracticePresence();
                // ゲームのシーンが届くまで（歯磨き粉の幕の下で）出したまま。中止されたら消える。
                PracticeView.Play(settings, _practice, LocalSeat,
                    () => IsOnline && !IsGameLoaded && (Roster.phase == LobbyPhase.Practice || Roster.phase == LobbyPhase.Loading));
            }
            else if (before == LobbyPhase.Practice && after != LobbyPhase.Practice)
            {
                _practiceRunning = false;
            }
        }

        private void TickPractice(float now, float dt)
        {
            if (!_practiceRunning)
            {
                return;
            }

            var manager = NetworkManager.Singleton;
            if (!IsOnline || manager == null || Roster.phase != LobbyPhase.Practice)
            {
                _practiceRunning = false;
                return;
            }

            SyncPracticePresence();

            // 各PCのセンサーは、そのPCでは常に番号0として読む（キーボードなら Q）。ゲームの砲台と同じ。
            var provider = MS2026.GripInputBridge.GripInputBridge.Provider;
            var gripping = provider != null && provider.IsGripping(0);
            var grip = provider != null ? provider.GetGripValue(0) : 0f;
            if (_practice.finished)
            {
                _practiceTrainer.Complete();
            }

            _practiceTrainer.Tick(gripping, grip, dt, HowToPlay.rules);
            var seat = LocalSeat;
            _practice.SetSeat(seat, _practiceTrainer.Grip01, _practiceTrainer.Heat01, _practiceTrainer.Gripping, _practiceTrainer.Overheated, _practiceTrainer.Step);
            _practice.Tick(dt);

            if (now < _practiceNextSend)
            {
                return;
            }

            if (IsHost)
            {
                if (_practice.Version != _practiceSentVersion)
                {
                    _practiceSentVersion = _practice.Version;
                    _practiceNextSend = now + PracticeSendInterval;
                    PracticeMessages.BroadcastState(manager, _practice);
                }
            }
            else if (PracticeBoard.IsValid(seat))
            {
                var mine = _practice.seats[seat];
                if (!SamePracticeInput(mine, _practiceSentInput))
                {
                    _practiceSentInput = mine;
                    _practiceNextSend = now + PracticeSendInterval;
                    PracticeMessages.SendInput(manager, mine);
                }
            }
        }

        private void SyncPracticePresence()
        {
            for (var i = 0; i < PracticeBoard.SeatCount; i++)
            {
                _practice.SetPresent(i, Roster.seats[i].present);
            }
        }

        private static bool SamePracticeInput(PracticeBoard.Seat a, PracticeBoard.Seat b)
        {
            return a.grip == b.grip && a.heat == b.heat && a.gripping == b.gripping && a.overheated == b.overheated && a.step == b.step;
        }

        // ───────── 届いたメッセージ ─────────

        private void HandlePracticeInput(ulong sender, FastBufferReader reader)
        {
            if (!IsHost)
            {
                return;
            }

            var input = PracticeMessages.ReadInput(reader);
            var seat = Roster.SeatOf(sender);
            if (seat < 0 || Roster.phase != LobbyPhase.Practice)
            {
                return;
            }

            _practice.SetSeat(seat, input.Grip01, input.Heat01, input.gripping, input.overheated, input.step);
        }

        private void HandlePracticeState(ulong sender, FastBufferReader reader)
        {
            if (IsHost || Roster.phase != LobbyPhase.Practice)
            {
                return;
            }

            PracticeMessages.ReadState(reader, _practice, LocalSeat);
            if (_practice.finished)
            {
                _practiceTrainer.Complete();
            }
        }

        // ───────── 読み込み中のヒント ─────────

        /// <summary>
        /// ゲームへの幕に、ヒントのカードを足す: 覆いきったらカードを出し、読み込みが終わっても最低秒数たつまでは幕を上げない
        /// （部屋に戻るときは待たない）。幕が上がり始めたらカードも消える。
        /// </summary>
        private void AddLoadingTip(ref Action onCovered, ref Func<bool> isReady)
        {
            var settings = HowToPlay;
            if (!settings.tipEnabled)
            {
                return;
            }

            var coveredAt = -1f;
            var revealing = false;
            var covered = onCovered;
            var ready = isReady;
            onCovered = () =>
            {
                coveredAt = Time.unscaledTime;
                LoadingTipView.Show(settings, () => !revealing && IsOnline);
                covered?.Invoke();
            };
            isReady = () =>
            {
                if (revealing)
                {
                    return true;
                }

                var loaded = ready == null || ready();
                var aborted = Roster.phase == LobbyPhase.Waiting || !IsOnline;
                var shownLongEnough = coveredAt >= 0f && Time.unscaledTime - coveredAt >= settings.tipMinSeconds;
                revealing = loaded && (aborted || shownLongEnough);
                return revealing;
            };
        }
    }
}
