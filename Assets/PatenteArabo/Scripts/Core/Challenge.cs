using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if PA_UGS
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
#endif

namespace PatenteArabo
{
    public class RankEntry
    {
        public string PlayerId, Name, Country, Photo;
        public int Rank, Avatar, Level;
        public double Score;
        public bool Me, Gold;
    }

    /// <summary>
    /// Online challenge between learners (Unity Gaming Services Leaderboards).
    /// Leaderboards to create in the Unity Cloud dashboard (same ids):
    ///   weekly_study  (weekly reset, higher is better, keep latest)  - minutes studied this week
    ///   weekly_xp     (weekly reset, higher is better, keep latest)  - points this week
    ///   alltime_xp    (no reset,     higher is better, keep latest)  - total points
    /// </summary>
    public static class Challenge
    {
        public static readonly string[] Boards = { "weekly_xp", "weekly_study", "alltime_xp" };

        public static bool Available
        {
            get
            {
#if PA_UGS
                return true;
#else
                return false;
#endif
            }
        }

        static readonly HashSet<string> Blocked = new HashSet<string>(PlayerPrefs.GetString("pa_blocked", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

        public static void Block(string playerId)
        {
            Blocked.Add(playerId);
            PlayerPrefs.SetString("pa_blocked", string.Join(",", Blocked));
            PlayerPrefs.Save();
        }

        public static bool IsBlocked(string playerId) => Blocked.Contains(playerId);

        public static bool ShowPhotos
        {
            get => PlayerPrefs.GetInt("pa_show_photos", 0) == 1;
            set { PlayerPrefs.SetInt("pa_show_photos", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        static float _lastSubmit = -999;

#if PA_UGS
        static Task _init;
        static async Task Ensure()
        {
            if (_init == null) _init = Init();
            await _init;
        }

        static async Task Init()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static Dictionary<string, string> Meta(bool withPhoto)
        {
            var m = new Dictionary<string, string>
            {
                { "n", Profile.Name },
                { "c", Profile.Country },
                { "a", Profile.Avatar.ToString() },
                { "l", Gamify.Level.ToString() },
                { "g", Premium.HasGold ? "1" : "0" },
            };
            if (withPhoto && Profile.Avatar < 0) m["p"] = Profile.PhotoBase64();
            return m;
        }
#endif

        /// <summary>Sends my scores (at most once a minute unless forced).</summary>
        public static async void Submit(bool force = false)
        {
            if (!Profile.InChallenge || string.IsNullOrEmpty(Profile.Name)) return;
            if (!force && Time.realtimeSinceStartup - _lastSubmit < 60) return;
            _lastSubmit = Time.realtimeSinceStartup;
#if PA_UGS
            try
            {
                await Ensure();
                var scores = new (string id, double v)[]
                {
                    ("weekly_xp", Gamify.WeekXp),
                    ("weekly_study", Math.Floor(Gamify.WeekStudySeconds / 60.0)),
                    ("alltime_xp", Gamify.Xp),
                };
                foreach (var s in scores)
                {
                    try { await LeaderboardsService.Instance.AddPlayerScoreAsync(s.id, s.v, new AddPlayerScoreOptions { Metadata = Meta(true) }); }
                    catch (Exception) { await LeaderboardsService.Instance.AddPlayerScoreAsync(s.id, s.v, new AddPlayerScoreOptions { Metadata = Meta(false) }); }
                }
            }
            catch (Exception e) { Debug.LogWarning("Challenge submit: " + e.Message); }
#else
            await Task.CompletedTask;
#endif
        }

        /// <summary>Top players of a board (plus me at the end if I am not in the top).</summary>
        public static async Task<List<RankEntry>> Top(string board, int limit = 50)
        {
            var list = new List<RankEntry>();
#if PA_UGS
            await Ensure();
            var page = await LeaderboardsService.Instance.GetScoresAsync(board, new GetScoresOptions { Limit = limit, IncludeMetadata = true });
            string myId = AuthenticationService.Instance.PlayerId;
            foreach (var e in page.Results) list.Add(ToEntry(e, myId));
            if (!list.Exists(x => x.Me) && Profile.InChallenge)
            {
                try
                {
                    var me = await LeaderboardsService.Instance.GetPlayerScoreAsync(board, new GetPlayerScoreOptions { IncludeMetadata = true });
                    list.Add(ToEntry(me, myId));
                }
                catch { }
            }
#else
            await Task.CompletedTask;
#endif
            return list;
        }

#if PA_UGS
        static RankEntry ToEntry(LeaderboardEntry e, string myId)
        {
            var r = new RankEntry { PlayerId = e.PlayerId, Rank = e.Rank + 1, Score = e.Score, Me = e.PlayerId == myId, Name = "?" };
            try
            {
                var m = MiniJson.ReadStringMap(e.Metadata);
                if (m.TryGetValue("n", out var n) && !string.IsNullOrEmpty(n)) r.Name = n;
                if (m.TryGetValue("c", out var c)) r.Country = c;
                if (m.TryGetValue("a", out var a)) int.TryParse(a, out r.Avatar);
                if (m.TryGetValue("l", out var l)) int.TryParse(l, out r.Level);
                if (m.TryGetValue("p", out var p)) r.Photo = p;
                if (m.TryGetValue("g", out var g)) r.Gold = g == "1";
            }
            catch { }
            return r;
        }
#endif

        /// <summary>Removes me from all boards and deletes my online account (GDPR).</summary>
        public static async Task Leave()
        {
            Profile.InChallenge = false;
#if PA_UGS
            try
            {
                await Ensure();
                foreach (var b in Boards)
                    try { await LeaderboardsService.Instance.DeletePlayerScoreAsync(b); } catch { }
                try { await AuthenticationService.Instance.DeleteAccountAsync(); } catch { }
                _init = null;
            }
            catch (Exception e) { Debug.LogWarning(e.Message); }
#else
            await Task.CompletedTask;
#endif
        }
    }
}
