using EkycInquiry.Models.ViewModel;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace EkycInquiry.Services
{
    public class EkycClient
    {
        private readonly HttpClient _client;
        private readonly EkycClientOptions _options;
        public EkycClient(HttpClient client, IOptions<EkycClientOptions> ClientOptions)
        {
            _client = client;
            _options = ClientOptions.Value;
        }

        public async Task<GenerateTokenResponse> GenerateToken()
        {
            try
            {
                string Url = $"{_options.BaseURL}/auth/realms/zain/protocol/openid-connect/token";
                Dictionary<string, string> Params = new Dictionary<string, string>();
                Params.Add("grant_type", "password");
                Params.Add("client_id", "wid-client");
                Params.Add("username", _options.Username);
                Params.Add("password", _options.Password);
                using (var message = new HttpRequestMessage())
                {
                    message.Method = HttpMethod.Post;
                    message.RequestUri = new Uri(Url);
                    message.Content = new FormUrlEncodedContent(Params);
                    var res = await _client.SendAsync(message);
                    var dataStr = await res.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<GenerateTokenResponse>(dataStr);
                }
            }
            catch(Exception ex)
            {
                StaticHelpers.Log(ex.Message + "\n" + ex.InnerException + "\n" + ex.StackTrace);
            }
            return null;
        }

        public async Task<Stream> GetMediaFile(string MediaUID, string access_token)
        {
            try
            {
                string Url = $"{_options.BaseURL}/wid/api/v1/consumer/media/{MediaUID}";
                using (var message = new HttpRequestMessage())
                {
                    message.Headers.Add("Authorization", $"Bearer {access_token}");
                    message.Method = HttpMethod.Get;
                    message.RequestUri = new Uri(Url);
                    var res = await _client.SendAsync(message);
                    var d = await res.Content.ReadAsStringAsync();
                    return await res.Content.ReadAsStreamAsync();
                }
            }
            catch (Exception ex)
            {
                StaticHelpers.Log(ex.Message + "\n" + ex.InnerException + "\n" + ex.StackTrace);
            }
            return null;

        }
    }

    public class GenerateTokenResponse
    {
        public string access_token { get; set; }
        public int expires_in { get; set; }
        public int refresh_expires_in { get; set; }
        public string refresh_token { get; set; }
        public string token_type { get; set; }
        [JsonProperty("not-before-policy")]
        public int not_before_policy { get; set; }
        public string session_state { get; set; }
        public string scope { get; set; }
    }
}
