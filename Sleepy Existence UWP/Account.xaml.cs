using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Sleepy_Existence
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Account : Page
    {
        ExistClient Exist = new ExistClient();

        public Account()
        {
            this.InitializeComponent();
        }

        async Task UpdateExistAccount()
        {
            if (Exist.HasCredentials)
            {
                textBlockExistAccount.Text = "(retrieving account...)";
                buttonExistLogIn.IsEnabled = false;
                buttonExistLogOut.IsEnabled = true;
                await UpdateExistAccountName();
            }
            else
            {
                textBlockExistAccount.Text = "(none)";
                buttonExistLogIn.IsEnabled = true;
                buttonExistLogOut.IsEnabled = false;
            }
        }

        async Task UpdateExistAccountName()
        {
            Debug.Assert(Exist.HasCredentials, "Must have Exist credentials to get account name");

            try
            {
                await Exist.MaybeRefreshTokens();
            }
            catch (ExistException error)
            {
                await new MessageDialog(error.Message, "Refresh tokens failed").ShowAsync();
            }

            var response = await Exist.Http.GetAsync(new Uri("https://exist.io/api/1/users/$self/today/"));
            if (!response.IsSuccessStatusCode)
            {
                await new MessageDialog(response.RequestMessage.RequestUri.ToString(), response.StatusCode.ToString()).ShowAsync();
                textBlockExistAccount.Text = "(error)";
                return;
            }

            var today = JsonValue.Parse(await response.Content.ReadAsStringAsync());
            textBlockExistAccount.Text = $"{today.GetObject().GetNamedString("username")} ({today.GetObject().GetNamedString("first_name")} {today.GetObject().GetNamedString("last_name")})";
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await UpdateExistAccount();
        }

        private async void buttonExistLogIn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Exist.Authorize();
                await UpdateExistAccount();
            }
            catch (ExistException error)
            {
                await new MessageDialog(error.Message, "Authorize failed").ShowAsync();
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

            Exist.Deauthorize();
            await UpdateExistAccount();
        }
    }
}
