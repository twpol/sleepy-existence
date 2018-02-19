using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Credentials;
using Windows.System.Display;
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
    public sealed partial class Sleeping : Page
    {
        const string FormatTime = "HH:mm";

        DisplayRequest DisplayRequest;
        DateTimeOffset InBed = DateTimeOffset.MinValue;
        DateTimeOffset Awake = DateTimeOffset.MinValue;
        DateTimeOffset OutOfBed = DateTimeOffset.MinValue;
        int TimeToFallAsleepM;
        int Interruptions;

        DispatcherTimer Timer = new DispatcherTimer();

        public Sleeping()
        {
            this.InitializeComponent();

            DisplayRequest = new DisplayRequest();

            Timer.Tick += Timer_Tick;
            Timer.Interval = new TimeSpan(0, 0, 61 - DateTimeOffset.Now.Second);
            Timer.Start();

            InBed = DateTimeOffset.Now;

            UpdateDisplay();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayRequest.RequestActive();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            DisplayRequest.RequestRelease();
        }

        private void Timer_Tick(object sender, object e)
        {
            Timer.Interval = new TimeSpan(0, 0, 61 - DateTimeOffset.Now.Second);
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            var asleep = InBed.AddMinutes(TimeToFallAsleepM);

            textBlockClock.Text = DateTimeOffset.Now.ToString(FormatTime);

            textBlockInBed.Text = InBed.ToString(FormatTime);
            textBlockAsleep.Text = asleep.ToString(FormatTime);
            textBlockAwake.Text = Awake.ToString(FormatTime);
            textBlockOutOfBed.Text = OutOfBed.ToString(FormatTime);
            textBlockInterruptions.Text = Interruptions.ToString();

            buttonSave.IsEnabled = InBed != DateTimeOffset.MinValue && Awake != DateTimeOffset.MinValue && OutOfBed != DateTimeOffset.MinValue;
        }

        private void buttonInBedPlus_Click(object sender, RoutedEventArgs e)
        {
            InBed = InBed.AddMinutes(1);
            UpdateDisplay();
        }

        private void buttonInBedMinus_Click(object sender, RoutedEventArgs e)
        {
            InBed = InBed.AddMinutes(-1);
            UpdateDisplay();
        }

        private void buttonAsleepPlus_Click(object sender, RoutedEventArgs e)
        {
            TimeToFallAsleepM += 5;
            UpdateDisplay();
        }

        private void buttonAsleepMinus_Click(object sender, RoutedEventArgs e)
        {
            TimeToFallAsleepM -= 5;
            if (TimeToFallAsleepM < 0)
                TimeToFallAsleepM = 0;
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

        private void buttonOutOfBedPlus_Click(object sender, RoutedEventArgs e)
        {
            if (OutOfBed == DateTimeOffset.MinValue)
                OutOfBed = DateTimeOffset.Now;
            else
                OutOfBed = OutOfBed.AddMinutes(1);
            UpdateDisplay();
        }

        private void buttonOutOfBedMinus_Click(object sender, RoutedEventArgs e)
        {
            if (OutOfBed == DateTimeOffset.MinValue)
                OutOfBed = DateTimeOffset.Now;
            else
                OutOfBed = OutOfBed.AddMinutes(-1);
            UpdateDisplay();
        }

        private void buttonInterruptionPlus_Click(object sender, RoutedEventArgs e)
        {
            Interruptions++;
            UpdateDisplay();
        }

        private void buttonInterruptionMinus_Click(object sender, RoutedEventArgs e)
        {
            Interruptions--;
            if (Interruptions < 0)
                Interruptions = 0;
            UpdateDisplay();
        }

        private async void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            var client = new HttpClient();
            var vault = new PasswordVault();
            var ExistAccessToken = vault.Retrieve("exist.io", "access_token");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ExistAccessToken.Password}");

            var jsonDate = JsonValue.CreateStringValue(DateTimeOffset.Now.ToString("yyyy-MM-dd"));
            var asleep = InBed.AddMinutes(TimeToFallAsleepM);
            var content = new HttpStringContent(new JsonArray() {
                new JsonObject() {
                    { "name", JsonValue.CreateStringValue("time_in_bed") },
                    { "date", jsonDate },
                    { "value", JsonValue.CreateStringValue((OutOfBed - InBed).TotalMinutes.ToString("F0")) }
                },
                new JsonObject() {
                    { "name", JsonValue.CreateStringValue("sleep") },
                    { "date", jsonDate },
                    { "value", JsonValue.CreateStringValue((Awake - asleep).TotalMinutes.ToString("F0")) }
                },
                new JsonObject() {
                    { "name", JsonValue.CreateStringValue("sleep_start") },
                    { "date", jsonDate },
                    { "value", JsonValue.CreateStringValue((asleep.TimeOfDay.TotalMinutes - 12 * 60).ToString("F0")) }
                },
                new JsonObject() {
                    { "name", JsonValue.CreateStringValue("sleep_end") },
                    { "date", jsonDate },
                    { "value", JsonValue.CreateStringValue(Awake.TimeOfDay.TotalMinutes.ToString("F0")) }
                },
                new JsonObject() {
                    { "name", JsonValue.CreateStringValue("sleep_awakenings") },
                    { "date", jsonDate },
                    { "value", JsonValue.CreateStringValue(Interruptions.ToString("F0")) }
                },
            }.Stringify(), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");

            var response = await client.PostAsync(new Uri("https://exist.io/api/1/attributes/update/"), content);
            if (!response.IsSuccessStatusCode)
            {
                await new MessageDialog(response.RequestMessage.RequestUri.ToString(), response.StatusCode.ToString()).ShowAsync();
                return;
            }

            var jsonResponse = JsonValue.Parse(await response.Content.ReadAsStringAsync());
            // TODO: Check result

            this.Frame.GoBack();
        }
    }
}
