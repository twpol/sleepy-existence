using System;
using Windows.Data.Json;
using Windows.Foundation.Metadata;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Sleepy_Existence
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Sleeping : Page
    {
        const string FormatTime = "HH:mm";
        const string FormatTimeSpan = @"hh\:mm";

        DisplayRequest DisplayRequest;
        DateTimeOffset Bedtime = DateTimeOffset.MinValue;
        TimeSpan AwakeTime;
        DateTimeOffset Awake = DateTimeOffset.MinValue;
        int Awakenings;

        DispatcherTimer Timer = new DispatcherTimer();
        Color? OldStatusBarForeground;

        public Sleeping()
        {
            this.InitializeComponent();

            DisplayRequest = new DisplayRequest();

            Timer.Tick += Timer_Tick;
            Timer.Interval = new TimeSpan(0, 0, 61 - DateTimeOffset.Now.Second);
            Timer.Start();

            Bedtime = DateTimeOffset.Now;

            UpdateDisplay();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayRequest.RequestActive();

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                OldStatusBarForeground = statusBar.ForegroundColor;
                statusBar.ForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
            }

            ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            DisplayRequest.RequestRelease();

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                statusBar.ForegroundColor = OldStatusBarForeground;
            }

            ApplicationView.GetForCurrentView().ExitFullScreenMode();
        }

        private void Timer_Tick(object sender, object e)
        {
            Timer.Interval = new TimeSpan(0, 0, 61 - DateTimeOffset.Now.Second);
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            textBlockClock.Text = DateTimeOffset.Now.ToString(FormatTime);

            textBlockInBed.Text = Bedtime.ToString(FormatTime);
            textBlockAwakeTime.Text = AwakeTime.ToString(FormatTimeSpan);
            textBlockAwake.Text = Awake.ToString(FormatTime);
            textBlockAwakenings.Text = Awakenings.ToString();

            buttonSave.IsEnabled = Bedtime != DateTimeOffset.MinValue && Awake != DateTimeOffset.MinValue;
            buttonSave.Visibility = buttonSave.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void buttonInBedPlus_Click(object sender, RoutedEventArgs e)
        {
            Bedtime = Bedtime.AddMinutes(1);
            UpdateDisplay();
        }

        private void buttonInBedMinus_Click(object sender, RoutedEventArgs e)
        {
            Bedtime = Bedtime.AddMinutes(-1);
            UpdateDisplay();
        }

        private void buttonAwakeTimePlus_Click(object sender, RoutedEventArgs e)
        {
            AwakeTime = AwakeTime.Add(TimeSpan.FromMinutes(5));
            UpdateDisplay();
        }

        private void buttonAwakeTimeMinus_Click(object sender, RoutedEventArgs e)
        {
            if (AwakeTime.TotalMinutes > 0)
                AwakeTime = AwakeTime.Subtract(TimeSpan.FromMinutes(5));
            UpdateDisplay();
        }

        private void buttonAwakePlus_Click(object sender, RoutedEventArgs e)
        {
            if (Awake == DateTimeOffset.MinValue)
                Awake = DateTimeOffset.Now;
            else
                Awake = Awake.AddMinutes(1);
            UpdateDisplay();
        }

        private void buttonAwakeMinus_Click(object sender, RoutedEventArgs e)
        {
            if (Awake == DateTimeOffset.MinValue)
                Awake = DateTimeOffset.Now;
            else
                Awake = Awake.AddMinutes(-1);
            UpdateDisplay();
        }

        private void buttonAwakeningsPlus_Click(object sender, RoutedEventArgs e)
        {
            Awakenings++;
            UpdateDisplay();
        }

        private void buttonAwakeningsMinus_Click(object sender, RoutedEventArgs e)
        {
            Awakenings--;
            if (Awakenings < 0)
                Awakenings = 0;
            UpdateDisplay();
        }

        private async void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            var exist = new ExistClient();

            try
            {
                await exist.MaybeRefreshTokens();
            }
            catch (ExistException error)
            {
                await new MessageDialog(error.Message, "Refresh tokens failed").ShowAsync();
            }

            var jsonDate = JsonValue.CreateStringValue(DateTimeOffset.Now.ToString("yyyy-MM-dd"));
            var content = new HttpStringContent(new JsonArray() {
                new JsonObject() {
                    { "name", JsonValue.CreateStringValue("time_in_bed") },
                    { "date", jsonDate },
                    { "value", JsonValue.CreateStringValue((Awake - Bedtime).TotalMinutes.ToString("F0")) }
                },
                new JsonObject() {
                    { "name", JsonValue.CreateStringValue("sleep") },
                    { "date", jsonDate },
                    { "value", JsonValue.CreateStringValue((Awake - Bedtime - AwakeTime).TotalMinutes.ToString("F0")) }
                },
                new JsonObject() {
                    { "name", JsonValue.CreateStringValue("sleep_start") },
                    { "date", jsonDate },
                    { "value", JsonValue.CreateStringValue(((Bedtime.TimeOfDay.TotalMinutes + 12 * 60) % (24 * 60)).ToString("F0")) }
                },
                new JsonObject() {
                    { "name", JsonValue.CreateStringValue("sleep_end") },
                    { "date", jsonDate },
                    { "value", JsonValue.CreateStringValue(Awake.TimeOfDay.TotalMinutes.ToString("F0")) }
                },
                new JsonObject() {
                    { "name", JsonValue.CreateStringValue("sleep_awakenings") },
                    { "date", jsonDate },
                    { "value", JsonValue.CreateStringValue(Awakenings.ToString("F0")) }
                },
            }.Stringify(), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");

            var response = await exist.Http.PostAsync(new Uri("https://exist.io/api/1/attributes/update/"), content);
            if (!response.IsSuccessStatusCode)
            {
                await new MessageDialog(response.RequestMessage.RequestUri.ToString(), response.StatusCode.ToString()).ShowAsync();
                return;
            }

            var jsonResponse = JsonValue.Parse(await response.Content.ReadAsStringAsync());
            var failed = jsonResponse.GetObject().GetNamedArray("failed");
            if (failed.Count > 0)
            {
                await new MessageDialog(failed.ToString(), "Failed to submit some values").ShowAsync();
                return;
            }

            this.Frame.GoBack();
        }
    }
}
