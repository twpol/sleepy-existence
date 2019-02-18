using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
                Http.DefaultRequestHeaders.Add("Authorization", $"Bearer {AccessToken.Password}");
            }
            // System.Runtime.InteropServices.COMException (0x80070490): Element not found.
            catch (COMException error) when ((uint)error.HResult == 0x80070490)
            {
                Http.DefaultRequestHeaders.Remove("Authorization");
            }
        }

        public bool HasCredentials
        {
            get => null != AccessToken;
        }

        public async void Authorize()
        {
            var client = new HttpClient();

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
                    throw new AuthorizeException(error);
                }

                var accessRequest = new HttpFormUrlEncodedContent(new Dictionary<string, string> {
                        { "grant_type", "authorization_code" },
                        { "code", code },
                        { "client_id", ExistClientData.ClientId },
                        { "client_secret", ExistClientData.ClientSecret }
                    });
                var accessResponse = await client.PostAsync(new Uri("https://exist.io/oauth2/access_token"), accessRequest);
                if (!accessResponse.IsSuccessStatusCode)
                {
                    throw new AuthorizeException($"{accessResponse.StatusCode.ToString()}\n{accessResponse.RequestMessage.RequestUri.ToString()}");
                }

                var accessResponseJson = JsonValue.Parse(await accessResponse.Content.ReadAsStringAsync());
                SetTokens(accessResponseJson.GetObject().GetNamedString("access_token"), accessResponseJson.GetObject().GetNamedString("refresh_token"));
            }
            else
            {
                throw new AuthorizeException($"Authentication response\nStatus = {webAuthenticationResult.ResponseStatus.ToString()}\nData = {webAuthenticationResult.ResponseData}");
            }
        }

        public void Deauthorize()
        {
            ClearTokens();
        }

        void SetTokens(string accessToken, string refreshToken)
        {
            Vault.Add(AccessToken = new PasswordCredential("exist.io", "access_token", accessToken));
            Vault.Add(RefreshToken = new PasswordCredential("exist.io", "refresh_token", refreshToken));
        }

        void ClearTokens()
        {
            Vault.Remove(AccessToken);
            Vault.Remove(RefreshToken);
            AccessToken = null;
            RefreshToken = null;
        }
    }
}
