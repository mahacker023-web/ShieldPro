
          using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AppBlocker
{
    public partial class MainWindow : Window
    {
        private bool _isGuardActive = false;
        private string _masterPassword = "1234";
        private List<string> _blockedProcesses = new List<string> { "chrome", "idman" };
        private HashSet<int> _processedPids = new HashSet<int>();
        private readonly string _hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");

        public MainWindow()
        {
            InitializeComponent();
            RefreshTargetList();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        // Navigation Management
        private void NavWeb_Click(object sender, RoutedEventArgs e) => SwitchTab(TabWeb, BtnWeb);
        private void NavSettings_Click(object sender, RoutedEventArgs e) => SwitchTab(TabSettings, BtnSettings);
        private void NavAbout_Click(object sender, RoutedEventArgs e) => SwitchTab(TabAbout, BtnAbout);

        private void SwitchTab(Border activeTab, Button activeButton)
        {
            TabWeb.Visibility = Visibility.Collapsed;
            TabSettings.Visibility = Visibility.Collapsed;
            TabAbout.Visibility = Visibility.Collapsed;

            BtnWeb.Background = Brushes.Transparent;
            BtnSettings.Background = Brushes.Transparent;
            BtnAbout.Background = Brushes.Transparent;

            activeTab.Visibility = Visibility.Visible;
            activeButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5E5"));
        }

        private void RefreshTargetList()
        {
            LstTargets.Items.Clear();
            foreach (var item in _blockedProcesses)
            {
                LstTargets.Items.Add($"🚫 App Process: {item}.exe");
            }
            if (ChkAdultBlock.IsChecked == true)
            {
                LstTargets.Items.Add("🌐 Web Domain: pornhub.com");
            }
        }

        private void AddTarget_Click(object sender, RoutedEventArgs e)
        {
            string newApp = TxtNewTarget.Text.Trim().ToLower().Replace(".exe", "");
            if (!string.IsNullOrEmpty(newApp) && !_blockedProcesses.Contains(newApp))
            {
                _blockedProcesses.Add(newApp);
                TxtNewTarget.Clear();
                RefreshTargetList();
                MessageBox.Show($"Added {newApp}.exe to restricted list!", "ShieldPro", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TxtMasterPassword.Password))
            {
                _masterPassword = TxtMasterPassword.Password;
                MessageBox.Show("Security Master Password updated successfully!", "ShieldPro", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Safe Background Guarding
        private async void ToggleGuard_Click(object sender, RoutedEventArgs e)
        {
            _isGuardActive = !_isGuardActive;

            if (_isGuardActive)
            {
                ApplyWebRestrictions();
                BtnToggleGuard.Content = "Deactivate Shielding";
                BtnToggleGuard.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D9381E"));
                MessageBox.Show("ShieldPro Guard Active!", "ShieldPro Active", MessageBoxButton.OK, MessageBoxImage.Information);

                await Task.Run(() => StartProcessMonitoring());
            }
            else
            {
                BtnToggleGuard.Content = "Activate Protection Guard";
                BtnToggleGuard.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0067C0"));
                _processedPids.Clear();
                MessageBox.Show("ShieldPro Guard Deactivated.", "ShieldPro", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ApplyWebRestrictions()
        {
            try
            {
                if (ChkAdultBlock.IsChecked == true)
                {
                    string rule = "\n127.0.0.1 pornhub.com\n127.0.0.1 www.pornhub.com\n";
                    File.AppendAllText(_hostsPath, rule);
                }
            }
            catch { }
        }

        private void StartProcessMonitoring()
        {
            while (_isGuardActive)
            {
                foreach (var target in _blockedProcesses)
                {
                    Process[] processes = Process.GetProcessesByName(target);
                    foreach (var proc in processes)
                    {
                        if (!_processedPids.Contains(proc.Id))
                        {
                            _processedPids.Add(proc.Id);
                            
                            // Non-Blocking Safe Execution
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                HandleUnauthorizedProcess(proc);
                            }));
                        }
                    }
                }
                Task.Delay(2500).Wait(); // Reasonable interval
            }
        }

        private void HandleUnauthorizedProcess(Process proc)
        {
            try
            {
                if (proc.HasExited) return;

                bool lockEnabled = ChkLockApps.IsChecked == true;

                if (!lockEnabled)
                {
                    proc.Kill();
                    return;
                }

                // Smooth Prompt UI
                var promptWindow = new Window
                {
                    Title = "ShieldPro Lock",
                    Width = 350,
                    Height = 170,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    WindowStyle = WindowStyle.ToolWindow,
                    ResizeMode = ResizeMode.NoResize,
                    Topmost = true
                };

                var stack = new StackPanel { Margin = new Thickness(15) };
                stack.Children.Add(new TextBlock 
                { 
                    Text = $"'{proc.ProcessName}.exe' requires Master Password:", 
                    FontWeight = FontWeights.Bold, 
                    Margin = new Thickness(0, 0, 0, 10) 
                });

                var passBox = new PasswordBox { Height = 30, Margin = new Thickness(0, 0, 0, 10) };
                stack.Children.Add(passBox);

                var btnUnlock = new Button 
                { 
                    Content = "Unlock Application", 
                    Height = 32, 
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0067C0")), 
                    Foreground = Brushes.White 
                };

                bool isUnlocked = false;
                btnUnlock.Click += (s, e) =>
                {
                    if (passBox.Password == _masterPassword)
                    {
                        isUnlocked = true;
                        promptWindow.Close();
                    }
                    else
                    {
                        MessageBox.Show("Incorrect Password!", "ShieldPro", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                stack.Children.Add(btnUnlock);
                promptWindow.Content = stack;
                
                promptWindow.ShowDialog();

                if (!isUnlocked)
                {
                    proc.Kill();
                }
            }
            catch { }
        }
    }
}
