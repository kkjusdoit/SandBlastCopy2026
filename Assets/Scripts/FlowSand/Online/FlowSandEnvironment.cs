using UnityEngine;

namespace FlowSand.Online
{
    public static class FlowSandEnvironment
    {
        private const string ApiOverrideKey = "FlowSand.ApiBaseUrl";

        public static string ApiBaseUrl
        {
            get
            {
                string overridden = PlayerPrefs.GetString(ApiOverrideKey, string.Empty).TrimEnd('/');
                if (!string.IsNullOrEmpty(overridden))
                {
                    return overridden;
                }

#if UNITY_EDITOR
                return "http://127.0.0.1:3100";
#else
                return "https://api.example.com";
#endif
            }
        }

        public static void SetApiOverride(string url)
        {
            PlayerPrefs.SetString(ApiOverrideKey, url?.TrimEnd('/') ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}
