using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace FlowSand.Online
{
    public sealed class FlowSandLeaderboardService
    {
        private const string PendingKey = "FlowSand.Online.PendingScores";
        private readonly FlowSandApiClient client;
        private readonly FlowSandAuthService auth;

        public FlowSandLeaderboardService(FlowSandApiClient client, FlowSandAuthService auth)
        {
            this.client = client;
            this.auth = auth;
        }

        public async Task InitializeAsync()
        {
            await auth.LoginAsync();
            await FlushPendingAsync();
        }

        public async Task<ScoreResponse> SubmitAsync(ScoreSubmission submission)
        {
            try
            {
                await auth.LoginAsync();
                return await SendWithReauthenticationAsync(submission);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Leaderboard submission queued: {exception.Message}");
                Queue(submission);
                return null;
            }
        }

        public async Task<LeaderboardResponse> GetLeaderboardAsync(int limit = 50)
        {
            await auth.LoginAsync();
            try
            {
                return await client.GetAsync<LeaderboardResponse>($"/api/v1/leaderboard?limit={Mathf.Clamp(limit, 1, 100)}");
            }
            catch (FlowSandApiException exception) when (exception.StatusCode == 401)
            {
                auth.ClearToken();
                await auth.LoginAsync(true);
                return await client.GetAsync<LeaderboardResponse>($"/api/v1/leaderboard?limit={Mathf.Clamp(limit, 1, 100)}");
            }
        }

        private async Task<ScoreResponse> SendWithReauthenticationAsync(ScoreSubmission submission)
        {
            try
            {
                return await client.PostAsync<ScoreSubmission, ScoreResponse>("/api/v1/scores", submission);
            }
            catch (FlowSandApiException exception) when (exception.StatusCode == 401)
            {
                auth.ClearToken();
                await auth.LoginAsync(true);
                return await client.PostAsync<ScoreSubmission, ScoreResponse>("/api/v1/scores", submission);
            }
        }

        private async Task FlushPendingAsync()
        {
            List<ScoreSubmission> pending = LoadPending();
            if (pending.Count == 0) return;
            List<ScoreSubmission> remaining = new();
            foreach (ScoreSubmission submission in pending)
            {
                try
                {
                    await SendWithReauthenticationAsync(submission);
                }
                catch
                {
                    remaining.Add(submission);
                }
            }
            SavePending(remaining);
        }

        private static void Queue(ScoreSubmission submission)
        {
            List<ScoreSubmission> pending = LoadPending();
            if (pending.Exists(item => item.runId == submission.runId)) return;
            pending.Add(submission);
            if (pending.Count > 20) pending.RemoveAt(0);
            SavePending(pending);
        }

        private static List<ScoreSubmission> LoadPending()
        {
            string json = PlayerPrefs.GetString(PendingKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return new List<ScoreSubmission>();
            PendingSubmissionList stored = JsonUtility.FromJson<PendingSubmissionList>(json);
            return stored?.items == null
                ? new List<ScoreSubmission>()
                : new List<ScoreSubmission>(stored.items);
        }

        private static void SavePending(List<ScoreSubmission> pending)
        {
            PendingSubmissionList stored = new() { items = pending.ToArray() };
            PlayerPrefs.SetString(PendingKey, JsonUtility.ToJson(stored));
            PlayerPrefs.Save();
        }
    }
}
