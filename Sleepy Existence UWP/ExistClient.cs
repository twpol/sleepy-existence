using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Security.Authentication.Web;
using Windows.Security.Credentials;
using Windows.Web.Http;

namespace Sleepy_Existence
{
    class ExistClient
    {
        readonly PasswordVault Vault = new PasswordVault();
        PasswordCredential AccessToken;
        PasswordCredential RefreshToken;

        public readonly HttpClient Http = new HttpClient();

        public ExistClient()
        {
            try
            {
                AccessToken = Vault.Retrieve("exist.io", "access_token");
                RefreshToken = Vault.Retrieve("exist.io", "refresh_token");
            }
            catch (COMException error) when ((uint)error.HResult == 0x80070490)
            {
                // System.Runtime.InteropServices.COMException (0x80070490): Element not found.
                // Occurs if there are no matching credentials (i.e. user isn't logged in).
            }
            UpdateHttp();
        }

        public bool HasCredentials
        {
            get => null != AccessToken;
        }

        public async void Authorize()
        {
            var endUri = new Uri("https://sleepyexistence.co.uk/authorize/done");
            var startUri = new Uri($"https://exist.io/oauth2/authorize?response_type=code&client_id={ExistClientData.ClientId}&redirect_uri={endUri.ToString()}&scope=read+write");

            var webAuthenticationResult = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, startUri, endUri);
            if (webAuthenticationResult.ResponseStatus == WebAuthenticationStatus.Success)
            {
                var responseUri = new Uri(webAuthenticationResult.ResponseData);
                var error = responseUri.Query.Split('&').FirstOrDefault(query => query.StartsWith("error="))?.Replace("error=", "");
                var code = responseUri.Query.Split('&').FirstOrDefault(query => query.StartsWith("code="))?.Replace("code=", "");
                if (error != null)
                {
                    throw new ExistException(error);
                }

                var accessRequest = new HttpFormUrlEncodedContent(new Dictionary<string, string> {
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "client_id", ExistClientData.ClientId },
                    { "client_secret", ExistClientData.ClientSecret }
                });
                await AccessRequest(accessRequest);
            }
            else
            {
                throw new ExistException($"Authentication response\nStatus = {webAuthenticationResult.ResponseStatus.ToString()}\nData = {webAuthenticationResult.ResponseData}");
            }
        }

        public async Task MaybeRefreshTokens()
        {
            var response = await Http.GetAsync(new Uri("https://exist.io/api/1/users/$self/today/"));
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var refreshRequest = new HttpFormUrlEncodedContent(new Dictionary<string, string> {
                    { "refresh_token", RefreshToken.Password },
                    { "grant_type", "refresh_token" },
                    { "client_id", ExistClientData.ClientId },
                    { "client_secret", ExistClientData.ClientSecret }
                });
                await AccessRequest(refreshRequest);
            }
        }

        public void Deauthorize()
        {
            ClearTokens();
        }

        async Task AccessRequest(HttpFormUrlEncodedContent accessRequest)
        {
            var accessResponse = await Http.PostAsync(new Uri("https://exist.io/oauth2/access_token"), accessRequest);
            if (!accessResponse.IsSuccessStatusCode)
            {
                throw new ExistException($"{accessResponse.StatusCode.ToString()}\n{accessResponse.RequestMessage.RequestUri.ToString()}");
            }

            var accessResponseJson = JsonValue.Parse(await accessResponse.Content.ReadAsStringAsync());
            SetTokens(accessResponseJson.GetObject().GetNamedString("access_token"), accessResponseJson.GetObject().GetNamedString("refresh_token"));
        }

        void SetTokens(string accessToken, string refreshToken)
        {
            if (AccessToken == null) Vault.Add(AccessToken = new PasswordCredential("exist.io", "access_token", ""));
            if (RefreshToken == null) Vault.Add(RefreshToken = new PasswordCredential("exist.io", "refresh_token", ""));
            AccessToken.Password = accessToken;
            RefreshToken.Password = refreshToken;
            UpdateHttp();
        }

        void ClearTokens()
        {
            if (AccessToken != null) Vault.Remove(AccessToken);
            if (RefreshToken != null) Vault.Remove(RefreshToken);
            AccessToken = null;
            RefreshToken = null;
            UpdateHttp();
        }

        void UpdateHttp()
        {
            if (HasCredentials)
            {
                Http.DefaultRequestHeaders.Add("Authorization", $"Bearer {AccessToken.Password}");
            }
            else
            {
                Http.DefaultRequestHeaders.Remove("Authorization");
            }
        }
    }
}
