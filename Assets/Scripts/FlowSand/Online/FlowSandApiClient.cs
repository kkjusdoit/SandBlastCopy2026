using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace FlowSand.Online
{
    public sealed class FlowSandApiException : Exception
    {
        public long StatusCode { get; }

        public FlowSandApiException(long statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }

    public sealed class FlowSandApiClient
    {
        private readonly string baseUrl;
        private string token;

        public FlowSandApiClient(string baseUrl)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
        }

        public void SetToken(string value) => token = value;

        public Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest payload)
        {
            return SendAsync<TResponse>("POST", path, JsonUtility.ToJson(payload));
        }

        public Task<TResponse> GetAsync<TResponse>(string path)
        {
            return SendAsync<TResponse>("GET", path, null);
        }

        private async Task<TResponse> SendAsync<TResponse>(string method, string path, string json)
        {
            using UnityWebRequest request = new($"{baseUrl}{path}", method);
            request.timeout = 6;
            request.downloadHandler = new DownloadHandlerBuffer();
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", $"Bearer {token}");
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = string.IsNullOrEmpty(request.downloadHandler.text)
                    ? request.error
                    : request.downloadHandler.text;
                throw new FlowSandApiException(request.responseCode, message);
            }

            return JsonUtility.FromJson<TResponse>(request.downloadHandler.text);
        }
    }
}
