using BitacorasAPI.Models;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace BitacorasAPI.Services
{
    public class SecurityService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://security-module.vercel.app/";

        public SecurityService()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        }

        /// <summary>
        /// llama a POST /login
        /// retorna los tokens (access_token y refresh_token)
        /// </summary>
        public async Task<SecurityTokens> LoginAsync(string email, string password)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "login");
            request.Headers.Add("email", email);
            request.Headers.Add("password", password);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var tokens = JsonConvert.DeserializeObject<SecurityTokens>(content);
            return tokens;
        }

        /// <summary>
        /// llama a GET /validate
        /// se espera que el body devuelva true o false
        /// </summary>
        public async Task<bool> ValidateTokenAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "validate");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return false;

            var content = await response.Content.ReadAsStringAsync();
            bool isValid = false;
            bool.TryParse(content, out isValid);
            return isValid;
        }

        /// <summary>
        /// llama a GET /refresh
        /// Retorna nuevos tokens o null si falla
        /// </summary>
        public async Task<SecurityTokens> RefreshTokenAsync(string refreshToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "refresh");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var tokens = JsonConvert.DeserializeObject<SecurityTokens>(content);
            return tokens;
        }
    }
}
