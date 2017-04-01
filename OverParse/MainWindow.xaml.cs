using Newtonsoft.Json.Linq;
using NHotkey;
using NHotkey.Wpf;
using OverParse.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace OverParse
{
    public partial class MainWindow : Window
    {
        private Log encounterlog;
        private List<Combatant> lastCombatants = new List<Combatant>();
        private List<string> sessionLogFilenames = new List<string>();
        private string lastStatus = "";
        private IntPtr hwndcontainer;
        List<Combatant> workingList;

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            // Get this window's handle
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            hwndcontainer = hwnd;
        }

        public MainWindow() {
            InitializeComponent();

            this.Dispatcher.UnhandledException += Panic;

            try { Directory.CreateDirectory("Logs"); } catch {
                MessageBox.Show(Properties.Resources.E0001, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            Directory.CreateDirectory("Debug");

            FileStream filestream = new FileStream("Debug\\log_" + string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + ".txt", FileMode.Create);
            var streamwriter = new StreamWriter(filestream);
            streamwriter.AutoFlush = true;
            Console.SetOut(streamwriter);
            Console.SetError(streamwriter);

            Console.WriteLine("OVERPARSE V." + Assembly.GetExecutingAssembly().GetName().Version);

            if (Properties.Settings.Default.UpgradeRequired && !Properties.Settings.Default.ResetInvoked) {
                Console.WriteLine("Upgrading settings");
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
            }

            Properties.Settings.Default.ResetInvoked = false;

            Console.WriteLine("Applying UI settings");
            Console.WriteLine(this.Top = Properties.Settings.Default.Top);
            Console.WriteLine(this.Left = Properties.Settings.Default.Left);
            Console.WriteLine(this.Height = Properties.Settings.Default.Height);
            Console.WriteLine(this.Width = Properties.Settings.Default.Width);

            bool outOfBounds = (this.Left <= SystemParameters.VirtualScreenLeft - this.Width) ||
                (this.Top <= SystemParameters.VirtualScreenTop - this.Height) ||
                (SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth <= this.Left) ||
                (SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight <= this.Top);

            if (outOfBounds) {
                Console.WriteLine("Window's off-screen, resetting");
                this.Top = 50;
                this.Left = 50;
            }

            Console.WriteLine(AutoEndEncounters.IsChecked = Properties.Settings.Default.AutoEndEncounters);
            Console.WriteLine(SetEncounterTimeout.IsEnabled = AutoEndEncounters.IsChecked);
            Console.WriteLine(SeparateZanverse.IsChecked = Properties.Settings.Default.SeparateZanverse);
            Console.WriteLine(SeparateTurret.IsChecked = Properties.Settings.Default.SeparateTurret);
            Console.WriteLine(SeparateAIS.IsChecked = Properties.Settings.Default.SeparateAIS);
            Console.WriteLine(ClickthroughMode.IsChecked = Properties.Settings.Default.ClickthroughEnabled);
            Console.WriteLine(LogToClipboard.IsChecked = Properties.Settings.Default.LogToClipboard);
            Console.WriteLine(AlwaysOnTop.IsChecked = Properties.Settings.Default.AlwaysOnTop);
            Console.WriteLine(AutoHideWindow.IsChecked = Properties.Settings.Default.AutoHideWindow);
            Console.WriteLine("Finished applying settings");

            ShowDamageGraph.IsChecked = Properties.Settings.Default.ShowDamageGraph; ShowDamageGraph_Click(null, null);
            ShowRawDPS.IsChecked = Properties.Settings.Default.ShowRawDPS; ShowRawDPS_Click(null, null);
            CompactMode.IsChecked = Properties.Settings.Default.CompactMode; CompactMode_Click(null, null);
            AnonymizeNames.IsChecked = Properties.Settings.Default.AnonymizeNames; AnonymizeNames_Click(null, null);
            HighlightYourDamage.IsChecked = Properties.Settings.Default.HighlightYourDamage; HighlightYourDamage_Click(null, null);
            HandleWindowOpacity(); HandleListOpacity(); SeparateAIS_Click(null, null);

            Console.WriteLine($"Launch method: {Properties.Settings.Default.LaunchMethod}");

            if (Properties.Settings.Default.Maximized) {
                WindowState = WindowState.Maximized;
            }

            Console.WriteLine("Initializing hotkeys");
            try {
                HotkeyManager.Current.AddOrReplace("End Encounter", Key.E, ModifierKeys.Control | ModifierKeys.Shift, EndEncounter_Key);
                HotkeyManager.Current.AddOrReplace("End Encounter (No log)", Key.R, ModifierKeys.Control | ModifierKeys.Shift, EndEncounterNoLog_Key);
                HotkeyManager.Current.AddOrReplace("Debug Menu", Key.F11, ModifierKeys.Control | ModifierKeys.Shift, DebugMenu_Key);
                HotkeyManager.Current.AddOrReplace("Always On Top", Key.A, ModifierKeys.Control | ModifierKeys.Shift, AlwaysOnTop_Key);
            } catch {
                Console.WriteLine("Hotkeys failed to initialize");
                MessageBox.Show(Properties.Resources.E0003, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            if (!SkillDictionary.GetInstance().Initialize(SkillDictionary.LanguageEnum.JA)) {
                if (File.Exists(SkillDictionary.SkillCSVName)) {
                    MessageBox.Show(Properties.Resources.E0004, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                } else {
                    MessageBox.Show(Properties.Resources.E0005, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            Console.WriteLine("Initializing default log");
            encounterlog = new Log(Properties.Settings.Default.Path);
            UpdateForm(null, null);

            Console.WriteLine("Initializing damageTimer");
            System.Windows.Threading.DispatcherTimer damageTimer = new System.Windows.Threading.DispatcherTimer();
            damageTimer.Tick += new EventHandler(UpdateForm);
            damageTimer.Interval = new TimeSpan(0, 0, 1);
            damageTimer.Start();

            Console.WriteLine("Initializing inactiveTimer");
            System.Windows.Threading.DispatcherTimer inactiveTimer = new System.Windows.Threading.DispatcherTimer();
            inactiveTimer.Tick += new EventHandler(HideIfInactive);
            inactiveTimer.Interval = TimeSpan.FromMilliseconds(200);
            inactiveTimer.Start();

            Console.WriteLine("Initializing logCheckTimer");
            System.Windows.Threading.DispatcherTimer logCheckTimer = new System.Windows.Threading.DispatcherTimer();
            logCheckTimer.Tick += new EventHandler(CheckForNewLog);
            logCheckTimer.Interval = new TimeSpan(0, 0, 10);
            logCheckTimer.Start();

            Console.WriteLine("Checking for release updates");
            try {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/tyronesama/overparse/releases/latest");
                request.UserAgent = "OverParse";
                WebResponse response = request.GetResponse();
                Stream dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();
                reader.Close();
                response.Close();
                JObject responseJSON = JObject.Parse(responseFromServer);
                string responseVersion = Version.Parse(responseJSON["tag_name"].ToString()).ToString();
                string thisVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

                while (thisVersion.Substring(Math.Max(0, thisVersion.Length - 2)) == ".0") {
                    thisVersion = thisVersion.Substring(0, thisVersion.Length - 2);
                }

                while (responseVersion.Substring(Math.Max(0, responseVersion.Length - 2)) == ".0") {
                    responseVersion = responseVersion.Substring(0, responseVersion.Length - 2);
                }

                Console.WriteLine($"JSON version: {responseVersion} / Assembly version: {thisVersion}");
                if (responseVersion != thisVersion) {
                    var message = string.Format(Properties.Resources.I0001, thisVersion, responseVersion);
                    MessageBoxResult result = MessageBox.Show(message, "OverParse Update", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes) {
                        Process.Start("https://github.com/TyroneSama/OverParse/releases/latest");
                        Environment.Exit(-1);
                    }
                }
            } catch (Exception ex) { Console.WriteLine($"Failed to update check: {ex.ToString()}"); }

            Console.WriteLine("End of MainWindow constructor");
        }

        private void HideIfInactive(object sender, EventArgs e) {
            if (!Properties.Settings.Default.AutoHideWindow)
                return;

            string title = WindowsServices.GetActiveWindowTitle();
            string[] relevant = { "OverParse", "OverParse Setup", "OverParse Error", "Encounter Timeout", "Phantasy Star Online 2" };

            if (!relevant.Contains(title)) {
                this.Opacity = 0;
            } else {
                HandleWindowOpacity();
            }
        }

        private void CheckForNewLog(object sender, EventArgs e) {
            DirectoryInfo directory = encounterlog.logDirectory;
            if (!directory.Exists) {
                return;
            }
            if (directory.GetFiles().Count() == 0) {
                return;
            }

            FileInfo log = directory.GetFiles().Where(f => Regex.IsMatch(f.Name, @"\d+\.csv")).OrderByDescending(f => f.Name).First();

            if (log.Name != encounterlog.filename) {
                Console.WriteLine($"Found a new log file ({log.Name}), switching...");
                encounterlog = new Log(Properties.Settings.Default.Path);
            }
        }

        private void Panic(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
            string errorMessage = string.Format(Properties.Resources.E0002, e.Exception.Message);
            Console.WriteLine("=== UNHANDLED EXCEPTION ===");
            Console.WriteLine(e.Exception.ToString());
            MessageBox.Show(errorMessage, "OverParse Error - 素晴らしく運がないね君は!", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(-1);
        }

        private void UpdatePlugin_Click(object sender, RoutedEventArgs e) {
            if (Properties.Settings.Default.LaunchMethod == "Tweaker") {
                MessageBox.Show(Properties.Resources.E0006);
                return;
            }
            encounterlog.UpdatePlugin(Properties.Settings.Default.Path);
            EndEncounterNoLog_Click(this, null);
        }

        private void ResetLogFolder_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.Path = "Z:\\OBVIOUSLY\\BROKEN\\DEFAULT\\PATH";
            EndEncounterNoLog_Click(this, null);
        }

        private void ResetOverParse(object sender, RoutedEventArgs e) {
            MessageBoxResult result = MessageBox.Show(Properties.Resources.I0002, "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result != MessageBoxResult.Yes)
                return;

            Console.WriteLine("Resetting");
            Properties.Settings.Default.Reset();
            Properties.Settings.Default.ResetInvoked = true;
            Properties.Settings.Default.Save();

            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void EndEncounter_Key(object sender, HotkeyEventArgs e) {
            Console.WriteLine("Encounter hotkey pressed");
            EndEncounter_Click(null, null);
            e.Handled = true;
        }

        private void EndEncounterNoLog_Key(object sender, HotkeyEventArgs e) {
            Console.WriteLine("Encounter hotkey (no log) pressed");
            EndEncounterNoLog_Click(null, null);
            e.Handled = true;
        }

        private void AlwaysOnTop_Key(object sender, HotkeyEventArgs e) {
            Console.WriteLine("Always-on-top hotkey pressed");
            AlwaysOnTop.IsChecked = !AlwaysOnTop.IsChecked;
            IntPtr wasActive = WindowsServices.GetForegroundWindow();

            // hack for activating overparse window
            this.WindowState = WindowState.Minimized;
            this.Show();
            this.WindowState = WindowState.Normal;

            this.Topmost = AlwaysOnTop.IsChecked;
            AlwaysOnTop_Click(null, null);
            WindowsServices.SetForegroundWindow(wasActive);
            e.Handled = true;
        }

        private void DebugMenu_Key(object sender, HotkeyEventArgs e) {
            Console.WriteLine("Debug hotkey pressed");
            DebugMenu.Visibility = Visibility.Visible;
            e.Handled = true;
        }

        private void AutoHideWindow_Click(object sender, RoutedEventArgs e) {
            if (AutoHideWindow.IsChecked && Properties.Settings.Default.AutoHideWindowWarning) {
                MessageBox.Show(Properties.Resources.I0003, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                Properties.Settings.Default.AutoHideWindowWarning = false;
            }
            Properties.Settings.Default.AutoHideWindow = AutoHideWindow.IsChecked;
        }

        private void ClickthroughToggle(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.ClickthroughEnabled = ClickthroughMode.IsChecked;
        }

        private void ShowDamageGraph_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.ShowDamageGraph = ShowDamageGraph.IsChecked;
            UpdateForm(null, null);
        }

        private void ShowRawDPS_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.ShowRawDPS = ShowRawDPS.IsChecked;
            DPSColumn.Header = ShowRawDPS.IsChecked ? "DPS" : "%";
            UpdateForm(null, null);
        }

        private void AlwaysOnTop_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.AlwaysOnTop = AlwaysOnTop.IsChecked;
            this.OnActivated(e);
        }

        private void WindowOpacity_0_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.WindowOpacity = 0;
            HandleWindowOpacity();
        }

        private void WindowOpacity_25_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.WindowOpacity = .25;
            HandleWindowOpacity();
        }

        private void WindowOpacity_50_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.WindowOpacity = .50;
            HandleWindowOpacity();
        }

        private void WindowOpacity_75_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.WindowOpacity = .75;
            HandleWindowOpacity();
        }

        private void WindowOpacity_100_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.WindowOpacity = 1;
            HandleWindowOpacity();
        }

        public void HandleWindowOpacity() {
            TheWindow.Opacity = Properties.Settings.Default.WindowOpacity;
            // ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG
            WinOpacity_0.IsChecked = false;
            WinOpacity_25.IsChecked = false;
            Winopacity_50.IsChecked = false;
            WinOpacity_75.IsChecked = false;
            WinOpacity_100.IsChecked = false;

            if (Properties.Settings.Default.WindowOpacity == 0) {
                WinOpacity_0.IsChecked = true;
            } else if (Properties.Settings.Default.WindowOpacity == .25) {
                WinOpacity_25.IsChecked = true;
            } else if (Properties.Settings.Default.WindowOpacity == .50) {
                Winopacity_50.IsChecked = true;
            } else if (Properties.Settings.Default.WindowOpacity == .75) {
                WinOpacity_75.IsChecked = true;
            } else if (Properties.Settings.Default.WindowOpacity == 1) {
                WinOpacity_100.IsChecked = true;
            }
        }

        // HAHAHAHAHAHAHAHAHAHAHAHAHA

        private void ListOpacity_0_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.ListOpacity = 0;
            HandleListOpacity();
        }

        private void ListOpacity_25_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.ListOpacity = .25;
            HandleListOpacity();
        }

        private void ListOpacity_50_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.ListOpacity = .50;
            HandleListOpacity();
        }

        private void ListOpacity_75_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.ListOpacity = .75;
            HandleListOpacity();
        }

        private void ListOpacity_100_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.ListOpacity = 1;
            HandleListOpacity();
        }

        public void HandleListOpacity() {
            WinBorderBackground.Opacity = Properties.Settings.Default.ListOpacity;
            ListOpacity_0.IsChecked = false;
            ListOpacity_25.IsChecked = false;
            Listopacity_50.IsChecked = false;
            ListOpacity_75.IsChecked = false;
            ListOpacity_100.IsChecked = false;

            if (Properties.Settings.Default.ListOpacity == 0) {
                ListOpacity_0.IsChecked = true;
            } else if (Properties.Settings.Default.ListOpacity == .25) {
                ListOpacity_25.IsChecked = true;
            } else if (Properties.Settings.Default.ListOpacity == .50) {
                Listopacity_50.IsChecked = true;
            } else if (Properties.Settings.Default.ListOpacity == .75) {
                ListOpacity_75.IsChecked = true;
            } else if (Properties.Settings.Default.ListOpacity == 1) {
                ListOpacity_100.IsChecked = true;
            }
        }

        private void HighlightYourDamage_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.HighlightYourDamage = HighlightYourDamage.IsChecked;
            UpdateForm(null, null);
        }

        private void AnonymizeNames_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.AnonymizeNames = AnonymizeNames.IsChecked;
            UpdateForm(null, null);
        }

        private void CompactMode_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.CompactMode = CompactMode.IsChecked;
            if (CompactMode.IsChecked) {
                MaxHitHelperColumn.Width = new GridLength(0, GridUnitType.Star);
            } else {
                MaxHitHelperColumn.Width = new GridLength(3, GridUnitType.Star);
            }
            UpdateForm(null, null);
        }

        private void Window_Deactivated(object sender, EventArgs e) {
            Window window = (Window)sender;
            window.Topmost = AlwaysOnTop.IsChecked;
            if (Properties.Settings.Default.ClickthroughEnabled) {
                int extendedStyle = WindowsServices.GetWindowLong(hwndcontainer, WindowsServices.GWL_EXSTYLE);
                WindowsServices.SetWindowLong(hwndcontainer, WindowsServices.GWL_EXSTYLE, extendedStyle | WindowsServices.WS_EX_TRANSPARENT);
            }
        }

        private void Window_StateChanged(object sender, EventArgs e) {
            if (this.WindowState == WindowState.Maximized) {
                this.WindowState = WindowState.Normal;
            }
        }

        private void Window_Activated(object sender, EventArgs e) {
            HandleWindowOpacity();
            Window window = (Window)sender;
            window.Topmost = AlwaysOnTop.IsChecked;
            if (Properties.Settings.Default.ClickthroughEnabled) {
                int extendedStyle = WindowsServices.GetWindowLong(hwndcontainer, WindowsServices.GWL_EXSTYLE);
                WindowsServices.SetWindowLong(hwndcontainer, WindowsServices.GWL_EXSTYLE, extendedStyle & ~WindowsServices.WS_EX_TRANSPARENT);
            }
        }

        public void UpdateForm(object sender, EventArgs e) {
            if (encounterlog == null) {
                return;
            }

            encounterlog.UpdateLog(this, null);
            EncounterStatus.Content = encounterlog.logStatus();

            // every part of this section is fucking stupid

            // 戦闘データをコピー(何のためかはよく分かってない)
            var targetList = (encounterlog.running ? encounterlog.combatants : lastCombatants);
            workingList = targetList.Select(c => new Combatant(c)).ToList();

            // フォーム表示をクリア
            CombatantData.Items.Clear();

            // for zanverse dummy and status bar because WHAT IS GOOD STRUCTURE
            int elapsed = 0;
            Combatant stealActiveTimeDummy = workingList.FirstOrDefault();
            if (stealActiveTimeDummy != null)
                elapsed = stealActiveTimeDummy.ActiveTime;

            // AISのダメージを分離
            if (Properties.Settings.Default.SeparateAIS) {
                var pendingCombatants = new List<Combatant>();
                foreach (Combatant c in workingList.Where(c => c.IsAlly && c.AISDamage > 0)) {
                    var holder = new Combatant(c.ID, $"AIS|{c.Name}", Combatant.TemporaryEnum.IS_AIS);
                    var targetAttacks = c.Attacks.Where(a => Combatant.AISAttackIDs.Contains(a.ID)).ToList();
                    holder.Attacks.AddRange(targetAttacks);
                    c.Attacks = c.Attacks.Except(targetAttacks).ToList();
                    holder.ActiveTime = elapsed;
                    pendingCombatants.Add(holder);
                }
                workingList.AddRange(pendingCombatants);
                workingList.Sort((x, y) => y.ReadDamage.CompareTo(x.ReadDamage));
            }

            // 銃座のダメージを分離
            if (Properties.Settings.Default.SeparateZanverse && workingList.Any(c => c.IsAlly && c.TurretDamage > 0)) {
                var holder = new Combatant("99999998", "Turret", Combatant.TemporaryEnum.IS_TURRET);
                foreach (var c in workingList.Where(c => c.IsAlly)) {
                    var targetAttacks = c.Attacks.Where(a => Combatant.TurretAttakIDs.Contains(a.ID)).ToList();
                    holder.Attacks.AddRange(targetAttacks);
                    c.Attacks = c.Attacks.Except(targetAttacks).ToList();
                }
                holder.ActiveTime = elapsed;
                workingList.Add(holder);
            }

            // ザンバースのダメージを分離
            if (Properties.Settings.Default.SeparateZanverse && workingList.Any(c => c.IsAlly && c.ZanverseDamage > 0)) {
                var holder = new Combatant("99999999", "Zanverse", Combatant.TemporaryEnum.IS_ZANVERSE);
                foreach (var c in workingList.Where(c => c.IsAlly)) {
                    var targetAttacks = c.Attacks.Where(a => a.ID == Combatant.ZanverseID).ToList();
                    holder.Attacks.AddRange(targetAttacks);
                    c.Attacks = c.Attacks.Except(targetAttacks).ToList();
                }
                holder.ActiveTime = elapsed;
                workingList.Add(holder);
            }

            // get group damage totals
            int totalReadDamage = workingList.Where(c => (c.IsAlly || c.IsZanverse || c.IsTurret)).Sum(c => c.ReadDamage);

            // dps calcs!
            foreach (Combatant c in workingList) {
                if (c.IsAlly || c.IsZanverse || c.IsTurret) {
                    c.PercentReadDPS = c.ReadDamage * 100f / totalReadDamage;
                } else {
                    c.PercentReadDPS = -1;
                }
            }

            // damage graph stuff
            Combatant.maxShare = 0;
            foreach (Combatant c in workingList) {
                if ((c.IsAlly) && c.ReadDamage > Combatant.maxShare)
                    Combatant.maxShare = c.ReadDamage;

                bool filtered = true;
                if (Properties.Settings.Default.SeparateAIS) {
                    if (c.IsAlly && c.IsNotTemporary && !HidePlayers.IsChecked) {
                        filtered = false;
                    } else if (c.IsAlly && c.IsAIS && !HideAIS.IsChecked) {
                        filtered = false;
                    } else if (c.IsZanverse || c.IsTurret) {
                        filtered = false;
                    }
                } else {
                    // 
                    if (c.IsAlly || c.IsZanverse || c.IsTurret) {
                        filtered = false;
                    }
                }
                if (!filtered && c.Damage > 0) {
                    CombatantData.Items.Add(c);
                }
            }

            // status pane updates
            EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 100, 100));
            EncounterStatus.Content = encounterlog.logStatus();

            if (encounterlog.valid && encounterlog.notEmpty) {
                EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 255, 0));
                EncounterStatus.Content = $"Waiting - {lastStatus}";
                if (lastStatus == "")
                    EncounterStatus.Content = "Waiting for combat data...";

                CombatantData.Items.Refresh();
            }

            if (encounterlog.running) {
                EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 100, 255, 100));

                TimeSpan timespan = TimeSpan.FromSeconds(elapsed);
                string timer = timespan.ToString(@"mm\:ss");
                EncounterStatus.Content = $"{timer}";

                float totalDPS = totalReadDamage / (float)elapsed;

                if (totalDPS > 0)
                    EncounterStatus.Content += $" - {totalDPS.ToString("N2")} DPS";

                if (Properties.Settings.Default.CompactMode)
                    foreach (Combatant c in workingList)
                        if (c.IsYou)
                            EncounterStatus.Content += $" - MAX: {c.MaxHitDamage.ToString("N0")}";

                lastStatus = EncounterStatus.Content.ToString();
            }

            // autoend
            if (encounterlog.running) {
                if (Properties.Settings.Default.AutoEndEncounters) {
                    int unixTimestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    if ((unixTimestamp - encounterlog.newTimestamp) >= Properties.Settings.Default.EncounterTimeout) {
                        Console.WriteLine("Automatically ending an encounter");
                        EndEncounter_Click(null, null);
                    }
                }
            }

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            Console.WriteLine("Closing...");

            if (!Properties.Settings.Default.ResetInvoked) {
                if (WindowState == WindowState.Maximized) {
                    Properties.Settings.Default.Top = RestoreBounds.Top;
                    Properties.Settings.Default.Left = RestoreBounds.Left;
                    Properties.Settings.Default.Height = RestoreBounds.Height;
                    Properties.Settings.Default.Width = RestoreBounds.Width;
                    Properties.Settings.Default.Maximized = true;
                } else {
                    Properties.Settings.Default.Top = this.Top;
                    Properties.Settings.Default.Left = this.Left;
                    Properties.Settings.Default.Height = this.Height;
                    Properties.Settings.Default.Width = this.Width;
                    Properties.Settings.Default.Maximized = false;
                }
            }

            encounterlog.WriteLog();

            Properties.Settings.Default.Save();
        }

        private void LogToClipboard_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.LogToClipboard = LogToClipboard.IsChecked;
        }

        private void EndEncounterNoLog_Click(object sender, RoutedEventArgs e) {
            Console.WriteLine("Ending encounter (no log)");
            bool temp = Properties.Settings.Default.AutoEndEncounters;
            Properties.Settings.Default.AutoEndEncounters = false;
            UpdateForm(null, null);
            Properties.Settings.Default.AutoEndEncounters = temp;
            Console.WriteLine("Reinitializing log");
            lastStatus = "";
            encounterlog = new Log(Properties.Settings.Default.Path);
            UpdateForm(null, null);
        }

        private void EndEncounter_Click(object sender, RoutedEventArgs e) {
            Console.WriteLine("Ending encounter");
            bool temp = Properties.Settings.Default.AutoEndEncounters;
            Properties.Settings.Default.AutoEndEncounters = false;
            UpdateForm(null, null); // I'M FUCKING STUPID
            Properties.Settings.Default.AutoEndEncounters = temp;
            encounterlog.backupCombatants = encounterlog.combatants;

            var workingListCopy = workingList.Select(c=> new Combatant(c)).ToList();
            Console.WriteLine("Saving last combatant list");
            lastCombatants = encounterlog.combatants;
            encounterlog.combatants = workingListCopy;
            string filename = encounterlog.WriteLog();
            if (filename != null) {
                if ((SessionLogs.Items[0] as MenuItem).Name == "SessionLogPlaceholder")
                    SessionLogs.Items.Clear();
                int items = SessionLogs.Items.Count;

                string prettyName = filename.Split('/').LastOrDefault();

                sessionLogFilenames.Add(filename);

                var menuItem = new MenuItem() { Name = "SessionLog_" + items.ToString(), Header = prettyName };
                menuItem.Click += OpenRecentLog_Click;
                SessionLogs.Items.Add(menuItem);
            }
            if (Properties.Settings.Default.LogToClipboard) {
                encounterlog.WriteClipboard();
            }
            Console.WriteLine("Reinitializing log");
            encounterlog = new Log(Properties.Settings.Default.Path);
            UpdateForm(null, null);
        }

        private void OpenRecentLog_Click(object sender, RoutedEventArgs e) {
            string filename = sessionLogFilenames[SessionLogs.Items.IndexOf((e.OriginalSource as MenuItem))];
            Console.WriteLine($"attempting to open {filename}");
            Process.Start(Directory.GetCurrentDirectory() + "\\" + filename);
        }

        private void OpenLogsFolder_Click(object sender, RoutedEventArgs e) {
            Process.Start(Directory.GetCurrentDirectory() + "\\Logs");
        }

        private void AutoEndEncounters_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.AutoEndEncounters = AutoEndEncounters.IsChecked;
            SetEncounterTimeout.IsEnabled = AutoEndEncounters.IsChecked;
        }

        private void SeparateZanverse_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.SeparateZanverse = SeparateZanverse.IsChecked;
            UpdateForm(null, null);
        }

        private void SeparateTurret_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.SeparateTurret = SeparateTurret.IsChecked;
            UpdateForm(null, null);
        }

        private void SeparateAIS_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.SeparateAIS = SeparateAIS.IsChecked;
            HideAIS.IsEnabled = SeparateAIS.IsChecked;
            HidePlayers.IsEnabled = SeparateAIS.IsChecked;
            UpdateForm(null, null);
        }

        private void FilterPlayers_Click(object sender, RoutedEventArgs e) {
            UpdateForm(null, null);
        }

        private void HidePlayers_Click(object sender, RoutedEventArgs e) {
            if (HidePlayers.IsChecked)
                HideAIS.IsChecked = false;
            UpdateForm(null, null);
        }

        private void HideAIS_Click(object sender, RoutedEventArgs e) {
            if (HideAIS.IsChecked)
                HidePlayers.IsChecked = false;
            UpdateForm(null, null);
        }

        private void SetEncounterTimeout_Click(object sender, RoutedEventArgs e) {
            AlwaysOnTop.IsChecked = false;

            int x;
            string input = Microsoft.VisualBasic.Interaction.InputBox(Properties.Resources.I0004, "Encounter Timeout", Properties.Settings.Default.EncounterTimeout.ToString());
            if (Int32.TryParse(input, out x)) {
                if (x > 0) {
                    Properties.Settings.Default.EncounterTimeout = x;
                } else {
                    MessageBox.Show(Properties.Resources.E0007);
                }
            } else {
                if (input.Length > 0) {
                    MessageBox.Show(Properties.Resources.E0008);
                }
            }

            AlwaysOnTop.IsChecked = Properties.Settings.Default.AlwaysOnTop;
        }

        private void Placeholder_Click(object sender, RoutedEventArgs e) {
            MessageBox.Show(Properties.Resources.I0005);
        }

        private void About_Click(object sender, RoutedEventArgs e) {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            MessageBox.Show(string.Format(Properties.Resources.I0006, version), "OverParse");
        }

        private void Website_Click(object sender, RoutedEventArgs e) {
            Process.Start("http://www.tyronesama.moe/");
        }

        private void PSOWorld_Click(object sender, RoutedEventArgs e) {
            Process.Start("http://www.pso-world.com/forums/showthread.php?t=232386");
        }

        private void GitHub_Click(object sender, RoutedEventArgs e) {
            Process.Start("https://github.com/TyroneSama/OverParse");
        }

        private void EQSchedule_Click(object sender, RoutedEventArgs e) {
            Process.Start("https://calendar.google.com/calendar/embed?src=pso2emgquest@gmail.com&mode=agenda");
        }

        private void ArksLayer_Click(object sender, RoutedEventArgs e) {
            Process.Start("http://arks-layer.com/");
        }

        private void Bumped_Click(object sender, RoutedEventArgs e) {
            Process.Start("http://www.bumped.org/psublog/");
        }

        private void Fulldive_Click(object sender, RoutedEventArgs e) {
            Process.Start("http://www.fulldive.nu/");
        }

        private void ShamelessPlug_Click(object sender, RoutedEventArgs e) {
            Process.Start("http://twitch.tv/tyronesama");
        }

        private void SWiki_Click(object sender, RoutedEventArgs e) {
            Process.Start("http://pso2.swiki.jp/");
        }

        private void Exit_Click(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }

        private void GenerateFakeEntries_Click(object sender, RoutedEventArgs e) {
            encounterlog.GenerateFakeEntries();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void WindowStats_Click(object sender, RoutedEventArgs e) {
            string result = "";
            result += $"menu bar: {MenuBar.Width.ToString()} width {MenuBar.Height.ToString()} height\n";
            result += $"menu bar: {MenuBar.Padding} padding {MenuBar.Margin} margin\n";
            result += $"menu item: {MenuSystem.Width.ToString()} width {MenuSystem.Height.ToString()} height\n";
            result += $"menu item: {MenuSystem.Padding} padding {MenuSystem.Margin} margin\n";
            result += $"menu item: {AutoEndEncounters.Foreground} fg {AutoEndEncounters.Background} bg\n";
            result += $"menu item: {MenuSystem.FontFamily} {MenuSystem.FontSize} {MenuSystem.FontWeight} {MenuSystem.FontStyle}\n";
            result += $"image: {image.Width} w {image.Height} h {image.Margin} m\n";
            MessageBox.Show(result);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void CurrentLogFilename_Click(object sender, RoutedEventArgs e) {
            MessageBox.Show(encounterlog.filename);
        }
    }
}