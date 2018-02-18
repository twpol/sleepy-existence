using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Sleepy_Existence
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Sleeping : Page
    {
        const string FormatTimer = @"hh\:mm";
        const string FormatTime = "HH:mm";

        Brush ForegroundText = null;

        DateTimeOffset InBed;
        DateTimeOffset Awake;
        DateTimeOffset OutOfBed;
        int TimeToFallAsleepM;
        bool IsSleeping;

        DispatcherTimer Timer = new DispatcherTimer();

        public Sleeping()
        {
            this.InitializeComponent();

            ForegroundText = textBlockTimer.Foreground;

            Timer.Tick += Timer_Tick;
            Timer.Interval = new TimeSpan(0, 0, 1);
            Timer.Start();

            UpdateDisplay();
        }

        private void Timer_Tick(object sender, object e)
        {
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            var asleep = InBed.AddMinutes(TimeToFallAsleepM);

            textBlockTimer.Text = (IsSleeping ? DateTimeOffset.Now - InBed : OutOfBed - InBed).ToString(FormatTimer);
            textBlockInBed.Text = InBed.ToString(FormatTime);
            textBlockAsleep.Text = asleep.ToString(FormatTime);
            textBlockAwake.Text = Awake.ToString(FormatTime);
            textBlockOutOfBed.Text = OutOfBed.ToString(FormatTime);

            buttonSetInBed.IsEnabled = !IsSleeping;
            buttonSetAwake.IsEnabled = IsSleeping;
            buttonSetOutOfBed.IsEnabled = IsSleeping;
        }

        private void buttonSetInBed_Click(object sender, RoutedEventArgs e)
        {
            InBed = DateTimeOffset.Now;
            Awake = DateTimeOffset.MinValue;
            OutOfBed = DateTimeOffset.MinValue;
            IsSleeping = true;
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

        private void buttonSetAwake_Click(object sender, RoutedEventArgs e)
        {
            Awake = DateTimeOffset.Now;
            UpdateDisplay();
        }

        private void buttonSetOutOfBed_Click(object sender, RoutedEventArgs e)
        {
            OutOfBed = DateTimeOffset.Now;
            IsSleeping = false;
            UpdateDisplay();
        }
    }
}
