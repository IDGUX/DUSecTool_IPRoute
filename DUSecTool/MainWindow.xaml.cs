using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace DUSecTool_IPRoute
{
    public partial class MainWindow : Window
    {
        private List<long> responseTimes = new List<long>();
        private int packetLossCount = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void btnTrace_Click(object sender, RoutedEventArgs e)
        {
            string zielAdresse = txtZielAdresse.Text;
            ResultPanel.Children.Clear();
            responseTimes.Clear();
            packetLossCount = 0;

            if (!string.IsNullOrWhiteSpace(zielAdresse))
            {
                LoadingIndicator.Visibility = Visibility.Visible;

                await Task.Run(() =>
                {
                    bool pingSuccess = PerformPingTest(zielAdresse);
                    if (pingSuccess)
                    {
                        PerformTraceroute(zielAdresse);
                    }
                });

                LoadingIndicator.Visibility = Visibility.Collapsed;

                DisplaySummary();
            }
            else
            {
                MessageBox.Show("Bitte geben Sie eine gültige Zieladresse ein.");
            }
        }

        private bool PerformPingTest(string host)
        {
            AddResultLine("\nPing-Test:");

            try
            {
                Ping pingSender = new Ping();
                PingReply reply = pingSender.Send(host, 3000);

                if (reply.Status == IPStatus.Success)
                {
                    responseTimes.Add(reply.RoundtripTime);
                    string resultLine = $"Ping erfolgreich: {reply.Address} (Zeit: {reply.RoundtripTime}ms)";
                    AddResultLine(resultLine, GetColorForResponseTime(reply.RoundtripTime));
                    return true; 
                }
                else
                {
                    packetLossCount++;
                    AddResultLine($"Ping fehlgeschlagen: {reply.Status}", Brushes.Red);
                    MessageBox.Show("Ping zur Zieladresse fehlgeschlagen. Die Traceroute-Überprüfung wurde abgebrochen.");
                    return false; 
                }
            }
            catch (Exception ex)
            {
                packetLossCount++;
                AddResultLine($"Ping-Fehler: {ex.Message}", Brushes.Red);
                MessageBox.Show("Ping zur Zieladresse fehlgeschlagen. Die Traceroute-Überprüfung wurde abgebrochen.");
                return false; 
            }
        }

        private void PerformTraceroute(string host)
        {
            AddResultLine("\nTraceroute:");

            const int MaxHops = 30;
            const int Timeout = 3000;
            Ping pingSender = new Ping();

            for (int ttl = 1; ttl <= MaxHops; ttl++)
            {
                PingOptions options = new PingOptions(ttl, true);
                byte[] buffer = Encoding.ASCII.GetBytes("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
                PingReply reply = pingSender.Send(host, Timeout, buffer, options);

                if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                {
                    responseTimes.Add(reply.RoundtripTime);
                    string hopInfo = $"Hop {ttl}: {reply.Address} (Antwortzeit: {reply.RoundtripTime}ms)";
                    AddResultLineWithCopyButton(hopInfo, reply.Address.ToString(), GetColorForResponseTime(reply.RoundtripTime));

                    if (reply.Status == IPStatus.Success) break;
                }
                else
                {
                    packetLossCount++;
                    string hopInfo = $"Hop {ttl}: {reply.Status}";
                    AddResultLine(hopInfo, Brushes.Red);
                }
            }
        }

        private void DisplaySummary()
        {
            AddResultLine("\nZusammenfassung:", Brushes.Blue);

            long averageResponseTime = responseTimes.Count > 0 ? (long)responseTimes.Average() : 0;
            string responseTimeSummary = $"Durchschnittliche Antwortzeit: {averageResponseTime} ms";
            AddResultLine(responseTimeSummary, GetColorForResponseTime(averageResponseTime));

            string packetLossSummary = $"Paketverluste: {packetLossCount} Hops ohne Antwort";
            AddResultLine(packetLossSummary, packetLossCount > 0 ? Brushes.Red : Brushes.Green);

            string quality = (packetLossCount > 0) ? "Warnung: Paketverluste festgestellt." : "Verbindung ist stabil.";
            AddResultLine(quality, packetLossCount > 0 ? Brushes.Orange : Brushes.Green);
        }

        private void AddResultLine(string text)
        {
            AddResultLine(text, Brushes.Black); 
        }

        private void AddResultLine(string text, Brush color)
        {
            Dispatcher.Invoke(() =>
            {
                TextBlock textBlock = new TextBlock
                {
                    Text = text,
                    Foreground = color,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                
                AutomationProperties.SetName(textBlock, text);
                AutomationProperties.SetHelpText(textBlock, "Traceroute-Ergebniszeile");

                ResultPanel.Children.Add(textBlock);
            });
        }

        private void AddResultLineWithCopyButton(string text, string ipAddress)
        {
            AddResultLineWithCopyButton(text, ipAddress, Brushes.Black); 
        }

        private void AddResultLineWithCopyButton(string text, string ipAddress, Brush color)
        {
            Dispatcher.Invoke(() =>
            {
                StackPanel linePanel = new StackPanel { Orientation = Orientation.Horizontal };

                TextBlock textBlock = new TextBlock
                {
                    Text = text,
                    Foreground = color,
                    Margin = new Thickness(0, 0, 5, 0)
                };

               
                AutomationProperties.SetName(textBlock, text);
                AutomationProperties.SetHelpText(textBlock, "Traceroute-Ergebnis mit Kopierfunktion");

                Button copyButton = new Button
                {
                    Content = "Copy IP",
                    Margin = new Thickness(5, 0, 0, 0),
                    Padding = new Thickness(5, 2, 5, 2)
                };

                
                AutomationProperties.SetName(copyButton, $"Copy {ipAddress}");
                AutomationProperties.SetHelpText(copyButton, "Kopiert die IP-Adresse in die Zwischenablage");

                copyButton.Click += (s, args) =>
                {
                    Clipboard.SetText(ipAddress);
                    MessageBox.Show($"IP-Adresse {ipAddress} kopiert!");
                };

                linePanel.Children.Add(textBlock);
                linePanel.Children.Add(copyButton);

                ResultPanel.Children.Add(linePanel);
            });
        }

        private Brush GetColorForResponseTime(long responseTime)
        {
            if (responseTime < 100) return Brushes.Green;
            if (responseTime < 200) return Brushes.Orange;
            return Brushes.Red;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}
