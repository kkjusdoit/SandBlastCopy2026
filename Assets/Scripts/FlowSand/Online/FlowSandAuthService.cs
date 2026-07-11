using System;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace FlowSand.Online
{
    public sealed class FlowSandAuthService
    {
        private const string TokenKey = "FlowSand.Online.Token";
        private const string EditorPlayerKey = "FlowSand.Online.EditorPlayer";
        private readonly FlowSandApiClient client;
        private Task<OnlinePlayer> loginTask;

        public FlowSandAuthService(FlowSandApiClient client)
        {
            this.client = client;
        }

        public async Task<OnlinePlayer> LoginAsync(bool force = false)
        {
            if (!force)
            {
                string cached = PlayerPrefs.GetString(TokenKey, string.Empty);
                if (!string.IsNullOrEmpty(cached))
                {
                    client.SetToken(cached);
                    return null;
                }
            }

            if (loginTask != null)
            {
                return await loginTask;
            }

            loginTask = PerformLoginAsync();
            try
            {
                return await loginTask;
            }
            finally
            {
                loginTask = null;
            }
        }

        private async Task<OnlinePlayer> PerformLoginAsync()
        {

#if UNITY_WEBGL && !UNITY_EDITOR
            string code = await GetWechatCodeAsync();
            AuthResponse response = await client.PostAsync<AuthRequest, AuthResponse>(
                "/api/v1/auth/wechat",
                new AuthRequest { code = code });
#else
            string playerId = GetEditorPlayerId();
            AuthResponse response = await client.PostAsync<AuthRequest, AuthResponse>(
                "/api/v1/auth/dev",
                new AuthRequest { playerId = playerId });
#endif
            PlayerPrefs.SetString(TokenKey, response.token);
            PlayerPrefs.Save();
            client.SetToken(response.token);
            return response.player;
        }

        public void ClearToken()
        {
            client.SetToken(null);
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.Save();
        }

        private static string GetEditorPlayerId()
        {
            string id = PlayerPrefs.GetString(EditorPlayerKey, string.Empty);
            if (!string.IsNullOrEmpty(id)) return id;
            id = $"editor-{Guid.NewGuid():N}";
            PlayerPrefs.SetString(EditorPlayerKey, id);
            PlayerPrefs.Save();
            return id;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static Task<string> GetWechatCodeAsync()
        {
            TaskCompletionSource<string> completion = new();
            WX.Login(new LoginOption
            {
                timeout = 5000,
                success = result => completion.TrySetResult(result.code),
                fail = error => completion.TrySetException(new InvalidOperationException(error.errMsg)),
            });
            return completion.Task;
        }
#endif
    }
}
