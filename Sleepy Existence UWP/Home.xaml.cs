using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Authentication.Web;
using Windows.Security.Credentials;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Sleepy_Existence
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Home : Page
    {
        PasswordVault Vault = new PasswordVault();
        PasswordCredential ExistAccessToken;
        PasswordCredential ExistRefreshToken;

        HttpClient Client = new HttpClient();

        public Home()
        {
            this.InitializeComponent();
        }

        void UpdateExistAccount()
        {
            try
            {
                ExistAccessToken = Vault.Retrieve("exist.io", "access_token");
                ExistRefreshToken = Vault.Retrieve("exist.io", "refresh_token");
                Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ExistAccessToken.Password}");

                textBlockExistAccount.Text = "(retrieving account...)";
                buttonExistLogIn.IsEnabled = false;
                buttonExistLogOut.IsEnabled = true;

                UpdateExistAccountName();
            }
            catch (COMException error) when (error.HResult == -2147023728)
            {
                Client.DefaultRequestHeaders.Remove("Authorization");

                textBlockExistAccount.Text = "(none)";
                buttonExistLogIn.IsEnabled = true;
                buttonExistLogOut.IsEnabled = false;
            }
        }

        async void UpdateExistAccountName()
        {
            Debug.Assert(ExistAccessToken != null, "Must have Exist credentials to get account name");

            var response = await Client.GetAsync(new Uri("https://exist.io/api/1/users/$self/today/"));
            if (!response.IsSuccessStatusCode)
            {
                await new MessageDialog(response.RequestMessage.RequestUri.ToString(), response.StatusCode.ToString()).ShowAsync();
                textBlockExistAccount.Text = "(error)";
                return;
            }

            var today = JsonValue.Parse(await response.Content.ReadAsStringAsync());
            textBlockExistAccount.Text = $"{today.GetObject().GetNamedString("username")} ({today.GetObject().GetNamedString("first_name")} {today.GetObject().GetNamedString("last_name")})";
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateExistAccount();
        }

        private async void buttonExistLogIn_Click(object sender, RoutedEventArgs e)
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
                    await new MessageDialog(error, "Authoerize failed").ShowAsync();
                    return;
                }

                var accessRequest = new HttpFormUrlEncodedContent(new Dictionary<string, string> {
                        { "grant_type", "authorization_code" },
                        { "code", code },
                        { "client_id", ExistClientData.ClientId },
                        { "client_secret", ExistClientData.ClientSecret }
                    });
                var accessResponse = await Client.PostAsync(new Uri("https://exist.io/oauth2/access_token"), accessRequest);
                if (!accessResponse.IsSuccessStatusCode)
                {
                    await new MessageDialog(accessResponse.RequestMessage.RequestUri.ToString(), accessResponse.StatusCode.ToString()).ShowAsync();
                    return;
                }

                var accessResponseJson = JsonValue.Parse(await accessResponse.Content.ReadAsStringAsync());
                Vault.Add(new PasswordCredential("exist.io", "access_token", accessResponseJson.GetObject().GetNamedString("access_token")));
                Vault.Add(new PasswordCredential("exist.io", "refresh_token", accessResponseJson.GetObject().GetNamedString("refresh_token")));

                UpdateExistAccount();
            }
            else
            {
                await new MessageDialog($"Status = {webAuthenticationResult.ResponseStatus.ToString()}\nData = {webAuthenticationResult.ResponseData}", "Authentication response").ShowAsync();
            }
        }

        private async void buttonExistLogOut_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog("Do you want to log out of your Exist account?", "Log out");
            var commandLogOut = new UICommand("Log out");
            var commandCancel = new UICommand("Cancel");
            dialog.Commands.Add(commandLogOut);
            dialog.Commands.Add(commandCancel);
            dialog.DefaultCommandIndex = 1;
            var result = await dialog.ShowAsync();
            if (result != commandLogOut)
            {
                return;
            }

            Vault.Remove(ExistAccessToken);
            Vault.Remove(ExistRefreshToken);

            UpdateExistAccount();
        }
    }
}
