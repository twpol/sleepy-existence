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
        DateTimeOffset InBed;
        DateTimeOffset Awake;
        DateTimeOffset OutOfBed;
        int TimeToFallAsleepM;

        DispatcherTimer Timer = new DispatcherTimer();

        public Sleeping()
        {
            this.InitializeComponent();

            Timer.Tick += Timer_Tick;
            Timer.Interval = new TimeSpan(0, 0, 15);
            Timer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            var asleep = InBed.AddMinutes(TimeToFallAsleepM);

            textBlockTimer.Text = (DateTimeOffset.Now - InBed).ToString(@"hh\:mm");
            textBlockInBed.Text = InBed.ToString("HH:mm");
            textBlockAsleep.Text = asleep.ToString("HH:mm");
            textBlockAwake.Text = Awake.ToString("HH:mm");
            textBlockOutOfBed.Text = OutOfBed.ToString("HH:mm");
        }

        private void buttonSetInBed_Click(object sender, RoutedEventArgs e)
        {
            InBed = DateTimeOffset.Now;
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
            UpdateDisplay();
        }
    }
}
