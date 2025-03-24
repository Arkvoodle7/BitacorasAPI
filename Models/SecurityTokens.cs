using Newtonsoft.Json;

namespace BitacorasAPI.Models
{
    public class SecurityTokens
    {
        [JsonProperty("expires_in")]
        public long ExpiresIn { get; set; }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("usuarioID")]
        public string UsuarioID { get; set; }

        [JsonProperty("_id")]
        public string DocumentId { get; set; }
    }
}
