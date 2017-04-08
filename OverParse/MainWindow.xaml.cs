using Newtonsoft.Json.Linq;
using NHotkey.Wpf;
using OverParse.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace OverParse
{
    public partial class MainWindow : Window
    {
        private Log EncounterLog { get; set; }
        private List<Combatant> LastCombatants { get; set; } = new List<Combatant>();
        private string LastStatus { get; set; }
        private IntPtr hWnd { get; set; }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            hWnd = new WindowInteropHelper(this).Handle;
        }

        public MainWindow() {
            InitializeComponent();

            // 想定外の例外処理
            this.Dispatcher.UnhandledException += (sender, e) => {
                var errorMessage = string.Format(Properties.Resources.E0002, e.Exception.Message);
                Console.WriteLine("=== UNHANDLED EXCEPTION ===");
                Console.WriteLine(e.Exception.ToString());
                MessageBox.Show(errorMessage, "OverParse Error - 素晴らしく運がないね君は!", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(-1);
            };

            // ログ出力ディレクトリ作成
            try {
                Directory.CreateDirectory("Logs");
            } catch {
                MessageBox.Show(Properties.Resources.E0001, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            // デバッグファイル出力ディレクトリ作成
            Directory.CreateDirectory("Debug");

            // 標準出力・エラーをデバッグファイルに出力
            var dFileStream = new FileStream($"Debug\\log_{DateTime.Now:yyyy-MM-dd_hh-mm-ss-tt}.txt", FileMode.Create);
            var dStreamWriter = new StreamWriter(dFileStream);
            var dSyncWriter = TextWriter.Synchronized(dStreamWriter);
            dStreamWriter.AutoFlush = true;
            Console.SetOut(dSyncWriter);
            Console.SetError(dSyncWriter);

            // バージョン情報出力
            Console.WriteLine($"OVERPARSE V.{Assembly.GetExecutingAssembly().GetName().Version}");

            // 設定の更新
            if (Properties.Settings.Default.UpgradeRequired && !Properties.Settings.Default.ResetInvoked) {
                Console.WriteLine("Upgrading settings");
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
            }

            // 設定のリセットに関するフラグを更新
            Properties.Settings.Default.ResetInvoked = false;

            // ウィンドウ位置の初期化・出力
            Console.WriteLine("Applying UI settings");
            Console.WriteLine($"Top   : {this.Top = Properties.Settings.Default.Top}");
            Console.WriteLine($"Left  : {this.Left = Properties.Settings.Default.Left}");
            Console.WriteLine($"Height: {this.Height = Properties.Settings.Default.Height}");
            Console.WriteLine($"Width : {this.Width = Properties.Settings.Default.Width}");

            // ウィンドウが画面外にはみ出している場合に画面内に移動
            bool outOfBounds = (this.Left <= SystemParameters.VirtualScreenLeft - this.Width)
                || (this.Top <= SystemParameters.VirtualScreenTop - this.Height)
                || (SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth <= this.Left)
                || (SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight <= this.Top);
            if (outOfBounds) {
                Console.WriteLine("Window's off-screen, resetting");
                this.Top = 50;
                this.Left = 50;
            }

            // 各種設定の読込み・出力
            // -> Logging
            Console.WriteLine($"{nameof(AutoEndEncounters)}  : {AutoEndEncounters.IsChecked = Properties.Settings.Default.AutoEndEncounters}");
            Console.WriteLine($"{nameof(SetEncounterTimeout)}: {SetEncounterTimeout.IsEnabled = AutoEndEncounters.IsChecked}");
            // -> Parsing
            Console.WriteLine($"{nameof(SeparateZanverse)}   : {SeparateZanverse.IsChecked = Properties.Settings.Default.SeparateZanverse}");
            Console.WriteLine($"{nameof(SeparateTurret)}     : {SeparateTurret.IsChecked = Properties.Settings.Default.SeparateTurret}");
            Console.WriteLine($"{nameof(SeparateAIS)}        : {SeparateAIS.IsChecked = Properties.Settings.Default.SeparateAIS}");
            Console.WriteLine($"{nameof(HidePlayers)}        : {HidePlayers.IsChecked = Properties.Settings.Default.HidePlayers}");
            Console.WriteLine($"{nameof(HideAIS)}            : {HideAIS.IsChecked = Properties.Settings.Default.HideAIS}");
            Console.WriteLine($"{nameof(ShowRawDPS)}         : {ShowRawDPS.IsChecked = Properties.Settings.Default.ShowRawDPS}");
            Console.WriteLine($"{nameof(ShowDamageGraph)}    : {ShowDamageGraph.IsChecked = Properties.Settings.Default.ShowDamageGraph}");
            Console.WriteLine($"{nameof(AnonymizeNames)}     : {AnonymizeNames.IsChecked = Properties.Settings.Default.AnonymizeNames}");
            Console.WriteLine($"{nameof(SkillEN)}            : {SkillEN.IsChecked = Properties.Settings.Default.SkillLanguage == "EN"}");
            Console.WriteLine($"{nameof(SkillJA)}            : {SkillJA.IsChecked = Properties.Settings.Default.SkillLanguage == "JA"}");
            // -> Window
            Console.WriteLine($"{nameof(CompactMode)}        : {CompactMode.IsChecked = Properties.Settings.Default.CompactMode}");
            Console.WriteLine($"{nameof(HighlightYourDamage)}: {HighlightYourDamage.IsChecked = Properties.Settings.Default.HighlightYourDamage}");
            Console.WriteLine($"{nameof(AlwaysOnTop)}        : {AlwaysOnTop.IsChecked = Properties.Settings.Default.AlwaysOnTop}");
            Console.WriteLine($"{nameof(AutoHideWindow)}     : {AutoHideWindow.IsChecked = Properties.Settings.Default.AutoHideWindow}");
            Console.WriteLine($"{nameof(ClickthroughMode)}   : {ClickthroughMode.IsChecked = Properties.Settings.Default.ClickthroughEnabled}");
            Console.WriteLine("Finished applying settings");

            // 不透明度の反映
            HandleWindowOpacity();
            HandleListOpacity();

            // デバッグ用
            {
                var debugBeg = Properties.Settings.Default.DebugReadBegin;
                var debugEnd = Properties.Settings.Default.DebugReadEnd;
                if (debugBeg == 0 && debugEnd == 0) {
                    ReadDamagelogOffset.Header = "Line: All";
                } else {
                    ReadDamagelogOffset.Header = $"Line: {debugBeg}-{debugEnd}";
                }
            }

            // 追加で設定が必要な項目を無理矢理処理
            ParseMenu_Click(SeparateAIS, null);
            ParseMenu_Click(ShowRawDPS, null);
            WindowMenu_Click(CompactMode, null);

            // 起動モードの出力
            Console.WriteLine($"Launch method: {Properties.Settings.Default.LaunchMethod}");

            // ウィンドウ最大化
            if (Properties.Settings.Default.Maximized) {
                WindowState = WindowState.Maximized;
            }

            // ショートカットキーの設定
            Console.WriteLine("Initializing hotkeys");
            try {
                // Ctrl + Shift + E => DPS計測の終了(ログ出力)
                HotkeyManager.Current.AddOrReplace("End Encounter", Key.E, ModifierKeys.Control | ModifierKeys.Shift, (sender, e) => {
                    Console.WriteLine("Encounter hotkey pressed");
                    EndEncounterImpl();
                    e.Handled = true;
                });
                // Ctrl + Shift + R => DPS計測のリセット(ログ出力なし)
                HotkeyManager.Current.AddOrReplace("End Encounter (No log)", Key.R, ModifierKeys.Control | ModifierKeys.Shift, (sender, e) => {
                    Console.WriteLine("Encounter hotkey (no log) pressed");
                    EndEncounterNoLogImpl();
                    e.Handled = true;
                });
                // Ctrl + Shift + E => デバッグメニュー表示切替え
                HotkeyManager.Current.AddOrReplace("Debug Menu", Key.F11, ModifierKeys.Control | ModifierKeys.Shift, (sender, e) => {
                    Console.WriteLine("Debug hotkey pressed");
                    DebugMenu.Visibility = (DebugMenu.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
                    e.Handled = true;
                });
                // Ctrl + Shift + A => 最前面表示トグル
                HotkeyManager.Current.AddOrReplace("Always On Top", Key.A, ModifierKeys.Control | ModifierKeys.Shift, (sender, e) => {
                    Console.WriteLine("Always-on-top hotkey pressed");
                    AlwaysOnTop.IsChecked = !AlwaysOnTop.IsChecked;
                    IntPtr wasActive = WindowsServices.GetForegroundWindow();

                    // hack for activating overparse window
                    this.WindowState = WindowState.Minimized;
                    this.Show();
                    this.WindowState = WindowState.Normal;

                    this.Topmost = AlwaysOnTop.IsChecked;
                    AlwaysOnTopImpl(null, null);
                    WindowsServices.SetForegroundWindow(wasActive);
                    e.Handled = true;
                });
            } catch {
                Console.WriteLine("Hotkeys failed to initialize");
                MessageBox.Show(Properties.Resources.E0003, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // スキル辞書の初期化
            var language = SkillJA.IsChecked ? SkillDictionary.LanguageEnum.JA : SkillDictionary.LanguageEnum.EN;
            SkillDictionary.GetInstance().Initialize(language, (success, skillCsv) => {
                if (success) {
                    return;
                }
                if (skillCsv.Exists) {
                    MessageBox.Show(Properties.Resources.E0004, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                } else {
                    MessageBox.Show(Properties.Resources.E0005, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });

            // ログ読込み開始
            Console.WriteLine("Initializing default log");
            EncounterLog = new Log(Properties.Settings.Default.Path);
            foreach (var file in EncounterLog.Damagelogs()) {
                var item = new MenuItem() { Header = file.Name };
                item.Click += (sender, e) => {
                    EncounterLog.DebugLoadLog(file, Properties.Settings.Default.DebugReadBegin, Properties.Settings.Default.DebugReadEnd);
                };
                ReadDamagelog.Items.Add(item);
            }
            UpdateFormImpl();

            // タイマー設定
            Console.WriteLine("Initializing timers");
            Action<EventHandler, TimeSpan> SetTimer = (handler, interval) => {
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Tick += handler;
                timer.Interval = interval;
                timer.Start();
            };
            SetTimer(new EventHandler((sender, e) => UpdateFormImpl()), TimeSpan.FromSeconds(1)); // DPS計測の更新(1秒毎)
            SetTimer(new EventHandler(HideIfInactive), TimeSpan.FromSeconds(0.2));                // 非アクティブで隠す(0.2秒毎)
            SetTimer(new EventHandler(CheckForNewLog), TimeSpan.FromSeconds(10));                 // ログファイルの更新チェック(10秒毎)

            // 更新の確認(あとで)
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
            } catch (Exception ex) {
                Console.WriteLine($"Failed to update check: {ex.ToString()}");
            }

            // 初期化終了
            Console.WriteLine("End of MainWindow constructor");
        }

        // ***** ウィンドウイベント ***** //

        private void Window_Activated(object sender, EventArgs e) {
            HandleWindowOpacity();
            var window = (Window)sender;
            window.Topmost = AlwaysOnTop.IsChecked;
            if (Properties.Settings.Default.ClickthroughEnabled) {
                var extendedStyle = WindowsServices.GetWindowLong(hWnd, WindowsServices.GWL_EXSTYLE);
                WindowsServices.SetWindowLong(hWnd, WindowsServices.GWL_EXSTYLE, extendedStyle & ~WindowsServices.WS_EX_TRANSPARENT);
            }
        }

        private void Window_Deactivated(object sender, EventArgs e) {
            Window window = (Window)sender;
            window.Topmost = AlwaysOnTop.IsChecked;
            if (Properties.Settings.Default.ClickthroughEnabled) {
                int extendedStyle = WindowsServices.GetWindowLong(hWnd, WindowsServices.GWL_EXSTYLE);
                WindowsServices.SetWindowLong(hWnd, WindowsServices.GWL_EXSTYLE, extendedStyle | WindowsServices.WS_EX_TRANSPARENT);
            }
        }

        private void Window_StateChanged(object sender, EventArgs e) {
            if (this.WindowState == WindowState.Maximized) {
                this.WindowState = WindowState.Normal;
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
            EncounterLog.WriteLog();
            Properties.Settings.Default.Save();
        }

        // ***** 処理いろいろ ***** //

        public void SetStatus(string status, Nullable<Color> color = null) {
            if (color.HasValue) {
                EncounterIndicator.Fill = new SolidColorBrush(color.Value);
            }
            EncounterStatus.Content = status;
        }

        public void UpdateFormImpl() {
            if (EncounterLog == null) {
                return;
            }

            EncounterLog.UpdateLog();

            // 戦闘データをコピー(何のためかはよく分かってない)
            var work = (EncounterLog.Running ? EncounterLog.Combatants : LastCombatants).Separate().ToList();
            Combatant.Update(work);

            // フォーム表示を更新
            CombatantData.Items.Clear();
            foreach (var c in work.Select(c => new Combatant.FormBinder(c))) {
                CombatantData.Items.Add(c);
            }
            CombatantData.Items.Refresh();

            // ステータス表示の更新
            if (EncounterLog.Running) {
                var status = $"{Combatant.DisplayActiveTime} - {Combatant.DisplayTotalDamage} dmg - {Combatant.DisplayTotalDPS} DPS";
                if (Properties.Settings.Default.CompactMode) {
                    var target = work.FirstOrDefault(c => c.IsYou);
                    if (target != null) {
                        status += $" - MAX: {target.MaxHitDamage:N0}";
                    }
                }
                SetStatus(status, Color.FromArgb(255, 100, 255, 100));
                LastStatus = EncounterStatus.Content.ToString();
            } else if (EncounterLog.Valid && !EncounterLog.Empty) {
                var status = string.IsNullOrEmpty(LastStatus) ? "Waiting for combat data..." : $"Waiting - {LastStatus}";
                SetStatus(status, Color.FromArgb(255, 255, 255, 0));
            } else {
                var status = "00:00 - ∞ DPS";
                if (!EncounterLog.Valid) {
                    status = "USER SHOULD PROBABLY NEVER SEE THIS";
                } else  if (EncounterLog.Empty) {
                    status = "No logs: Enable plugin and check pso2_bin!";
                } else  if (!EncounterLog.Running) {
                    status = "Waiting for combat data...";
                }
                SetStatus(status, Color.FromArgb(255, 255, 100, 100));
            }

            // 自動終了(ログ出力とか)
            if (EncounterLog.Running && Properties.Settings.Default.AutoEndEncounters) {
                var unixTimestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                if ((unixTimestamp - EncounterLog.TimestampEnd) >= Properties.Settings.Default.EncounterTimeout) {
                    Console.WriteLine("Automatically ending an encounter");
                    EndEncounterImpl();
                }
            }
        }

        private void EndEncounterImpl() {
            Console.WriteLine("Ending encounter");
            EncounterLog.UpdateLog();
            if (!EncounterLog.Running) {
                return;
            }
            Console.WriteLine("Saving last combatant list");
            LastCombatants = EncounterLog.Combatants;
            // ログファイルを出力して一覧に追加
            var fileinfo = EncounterLog.WriteLog();
            if (fileinfo != null) {
                if ((SessionLogs.Items[0] as MenuItem).Name == "SessionLogPlaceholder") {
                    SessionLogs.Items.Clear();
                }
                var menuItem = new MenuItem() { Header = fileinfo.Name };
                menuItem.Click += (sender, e) => {
                    Console.WriteLine($"attempting to open {fileinfo.Name}");
                    Process.Start(fileinfo.FullName);
                };
                SessionLogs.Items.Add(menuItem);
            }
            // 初期化
            Console.WriteLine("Reinitializing log");
            EncounterLog = new Log(Properties.Settings.Default.Path);
            UpdateFormImpl();
        }

        public void EndEncounterNoLogImpl() {
            Console.WriteLine("Ending encounter (no log)");
            EncounterLog.UpdateLog();
            Console.WriteLine("Reinitializing log");
            EncounterLog = new Log(Properties.Settings.Default.Path);
            LastStatus = "";
            UpdateFormImpl();
        }

        private void AlwaysOnTopImpl(object sender, EventArgs e) {
            Properties.Settings.Default.AlwaysOnTop = AlwaysOnTop.IsChecked;
            this.OnActivated(e);
        }

        public void HandleWindowOpacity() {
            TheWindow.Opacity = Properties.Settings.Default.WindowOpacity;
            var items = new Dictionary<double, MenuItem>() {
                { 0.00, WinOpacity_0 },
                { 0.25, WinOpacity_25 },
                { 0.50, Winopacity_50 },
                { 0.75, WinOpacity_75 },
                { 1.00, WinOpacity_100 },
            };
            foreach (var item in items) {
                item.Value.IsChecked = (item.Key == Properties.Settings.Default.WindowOpacity);
            }
        }

        public void HandleListOpacity() {
            WinBorderBackground.Opacity = Properties.Settings.Default.ListOpacity;
            var items = new Dictionary<double, MenuItem>() {
                { 0.00, ListOpacity_0 },
                { 0.25, ListOpacity_25 },
                { 0.50, Listopacity_50 },
                { 0.75, ListOpacity_75 },
                { 1.00, ListOpacity_100 },
            };
            foreach (var item in items) {
                item.Value.IsChecked = (item.Key == Properties.Settings.Default.ListOpacity);
            }
        }

        // ***** その他イベント処理 ***** //

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void HideIfInactive(object sender, EventArgs e) {
            if (!Properties.Settings.Default.AutoHideWindow) {
                return;
            }
            var title = WindowsServices.GetActiveWindowTitle();
            var relevant = new string[] { "OverParse", "OverParse Setup", "OverParse Error", "Encounter Timeout", "Phantasy Star Online 2" };
            if (relevant.Contains(title)) {
                HandleWindowOpacity();
            } else {
                this.Opacity = 0;
            }
        }

        private void CheckForNewLog(object sender, EventArgs e) {
            var log = EncounterLog.LatestDamagelog();
            if (log != null && log.Name != EncounterLog.Filename) {
                Console.WriteLine($"Found a new log file ({log.Name}), switching...");
                EncounterLog = new Log(Properties.Settings.Default.Path);
            }
        }

        // ***** メニュー操作 ***** //

        /// <summary>
        /// ログメニューを選択した場合の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogMenu_Click(object sender, RoutedEventArgs e) {
            switch (((MenuItem)sender).Name) {
            case nameof(EndEncounter):
                EndEncounterImpl();
                break;
            case nameof(EndEncounterNoLog):
                EndEncounterNoLogImpl();
                break;
            case nameof(AutoEndEncounters):
                Properties.Settings.Default.AutoEndEncounters = AutoEndEncounters.IsChecked;
                SetEncounterTimeout.IsEnabled = AutoEndEncounters.IsChecked;
                break;
            case nameof(SetEncounterTimeout):
                AlwaysOnTop.IsChecked = false;
                var input = Microsoft.VisualBasic.Interaction.InputBox(Properties.Resources.I0004, "Encounter Timeout", Properties.Settings.Default.EncounterTimeout.ToString());
                try {
                    var i = int.Parse(input);
                    if (i > 0) {
                        Properties.Settings.Default.EncounterTimeout = i;
                    } else {
                        MessageBox.Show(Properties.Resources.E0007);
                    }
                } catch (Exception) {
                    if (input.Length > 0) {
                        MessageBox.Show(Properties.Resources.E0008);
                    }
                }
                AlwaysOnTop.IsChecked = Properties.Settings.Default.AlwaysOnTop;
                break;
            case nameof(OpenLogsFolder):
                Process.Start($"{Directory.GetCurrentDirectory()}\\Logs");
                break;
            }
        }

        /// <summary>
        /// パースメニューを選択した場合の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParseMenu_Click(object sender, RoutedEventArgs e) {
            switch (((MenuItem)sender).Name) {
            case nameof(SeparateZanverse):
                Properties.Settings.Default.SeparateZanverse = SeparateZanverse.IsChecked;
                UpdateFormImpl();
                break;
            case nameof(SeparateTurret):
                Properties.Settings.Default.SeparateTurret = SeparateTurret.IsChecked;
                UpdateFormImpl();
                break;
            case nameof(SeparateAIS):
                Properties.Settings.Default.SeparateAIS = SeparateAIS.IsChecked;
                HideAIS.IsEnabled = SeparateAIS.IsChecked;
                HidePlayers.IsEnabled = SeparateAIS.IsChecked;
                UpdateFormImpl();
                break;
            case nameof(HidePlayers):
                Properties.Settings.Default.HidePlayers = HidePlayers.IsChecked;
                if (HidePlayers.IsChecked) {
                    Properties.Settings.Default.HideAIS = false;
                    HideAIS.IsChecked = false;
                }
                UpdateFormImpl();
                break;
            case nameof(HideAIS):
                Properties.Settings.Default.HideAIS = HideAIS.IsChecked;
                if (HideAIS.IsChecked) {
                    Properties.Settings.Default.HidePlayers = false;
                    HidePlayers.IsChecked = false;
                }
                UpdateFormImpl();
                break;
            case nameof(ShowRawDPS):
                Properties.Settings.Default.ShowRawDPS = ShowRawDPS.IsChecked;
                DPSColumn.Header = ShowRawDPS.IsChecked ? "DPS" : "%";
                UpdateFormImpl();
                break;
            case nameof(ShowDamageGraph):
                Properties.Settings.Default.ShowDamageGraph = ShowDamageGraph.IsChecked;
                UpdateFormImpl();
                break;
            case nameof(AnonymizeNames):
                Properties.Settings.Default.AnonymizeNames = AnonymizeNames.IsChecked;
                UpdateFormImpl();
                break;
            case nameof(SkillEN):
                Properties.Settings.Default.SkillLanguage = "EN";
                SkillEN.IsChecked = true;
                SkillJA.IsChecked = false;
                SkillDictionary.GetInstance().Initialize(SkillDictionary.LanguageEnum.EN);
                UpdateFormImpl();
                break;
            case nameof(SkillJA):
                Properties.Settings.Default.SkillLanguage = "JA";
                SkillEN.IsChecked = false;
                SkillJA.IsChecked = true;
                SkillDictionary.GetInstance().Initialize(SkillDictionary.LanguageEnum.JA);
                UpdateFormImpl();
                break;
            }
        }

        /// <summary>
        /// ウィンドウメニューを選択した場合の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowMenu_Click(object sender, RoutedEventArgs e) {
            switch (((MenuItem)sender).Name) {
            case nameof(CompactMode):
                Properties.Settings.Default.CompactMode = CompactMode.IsChecked;
                if (CompactMode.IsChecked) {
                    MaxHitHelperColumn.Width = new GridLength(0, GridUnitType.Star);
                } else {
                    MaxHitHelperColumn.Width = new GridLength(3, GridUnitType.Star);
                }
                UpdateFormImpl();
                break;
            case nameof(HighlightYourDamage):
                Properties.Settings.Default.HighlightYourDamage = HighlightYourDamage.IsChecked;
                UpdateFormImpl();
                break;
            case nameof(WinOpacity_0):
                Properties.Settings.Default.WindowOpacity = 0.00;
                HandleWindowOpacity();
                break;
            case nameof(WinOpacity_25):
                Properties.Settings.Default.WindowOpacity = 0.25;
                HandleWindowOpacity();
                break;
            case nameof(Winopacity_50):
                Properties.Settings.Default.WindowOpacity = 0.50;
                HandleWindowOpacity();
                break;
            case nameof(WinOpacity_75):
                Properties.Settings.Default.WindowOpacity = 0.75;
                HandleWindowOpacity();
                break;
            case nameof(WinOpacity_100):
                Properties.Settings.Default.WindowOpacity = 1.00;
                HandleWindowOpacity();
                break;
            case nameof(ListOpacity_0):
                Properties.Settings.Default.ListOpacity = 0.00;
                HandleListOpacity();
                break;
            case nameof(ListOpacity_25):
                Properties.Settings.Default.ListOpacity = 0.25;
                HandleListOpacity();
                break;
            case nameof(Listopacity_50):
                Properties.Settings.Default.ListOpacity = 0.50;
                HandleListOpacity();
                break;
            case nameof(ListOpacity_75):
                Properties.Settings.Default.ListOpacity = 0.75;
                HandleListOpacity();
                break;
            case nameof(ListOpacity_100):
                Properties.Settings.Default.ListOpacity = 1.00;
                HandleListOpacity();
                break;
            case nameof(AlwaysOnTop):
                AlwaysOnTopImpl(sender, e);
                break;
            case nameof(AutoHideWindow):
                if (AutoHideWindow.IsChecked && Properties.Settings.Default.AutoHideWindowWarning) {
                    MessageBox.Show(Properties.Resources.I0003, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    Properties.Settings.Default.AutoHideWindowWarning = false;
                }
                Properties.Settings.Default.AutoHideWindow = AutoHideWindow.IsChecked;
                break;
            case nameof(ClickthroughMode):
                Properties.Settings.Default.ClickthroughEnabled = ClickthroughMode.IsChecked;
                break;
            }
        }

        /// <summary>
        /// デバッグメニューを選択した場合の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DebugMenu_Click(object sender, RoutedEventArgs e) {
            switch (((MenuItem)sender).Name) {
            case nameof(WindowStats):
                var result = new StringBuilder();
                result.AppendLine($"menu bar: {MenuBar.Width.ToString()} width {MenuBar.Height.ToString()} height");
                result.AppendLine($"menu bar: {MenuBar.Padding} padding {MenuBar.Margin} margin");
                result.AppendLine($"menu item: {MenuSystem.Width.ToString()} width {MenuSystem.Height.ToString()} height");
                result.AppendLine($"menu item: {MenuSystem.Padding} padding {MenuSystem.Margin} margin");
                result.AppendLine($"menu item: {AutoEndEncounters.Foreground} fg {AutoEndEncounters.Background} bg");
                result.AppendLine($"menu item: {MenuSystem.FontFamily} {MenuSystem.FontSize} {MenuSystem.FontWeight} {MenuSystem.FontStyle}");
                result.AppendLine($"image: {image.Width} w {image.Height} h {image.Margin} m");
                MessageBox.Show(result.ToString());
                break;
            case nameof(CurrentLogFilename):
                MessageBox.Show(EncounterLog.Filename);
                break;
            case nameof(ReadDamagelogOffset):
                var begin = Properties.Settings.Default.DebugReadBegin;
                var end = Properties.Settings.Default.DebugReadEnd;
                var input = Microsoft.VisualBasic.Interaction.InputBox("{begin},{end}", "OverParse", (begin == 0 && end == 0) ? "" : $"{begin},{end}");
                var values = input.Split(',').Select(s => s.Trim()).ToList();
                if (values.Count() == 2 && int.TryParse(values[0], out begin) && int.TryParse(values[1], out end) && begin > 0 && end >= begin) {
                    Properties.Settings.Default.DebugReadBegin = begin;
                    Properties.Settings.Default.DebugReadEnd = end;
                    ReadDamagelogOffset.Header = $"Line: {begin}-{end}";
                } else {
                    Properties.Settings.Default.DebugReadBegin = 0;
                    Properties.Settings.Default.DebugReadEnd = 0;
                    ReadDamagelogOffset.Header = "Line: All";
                }
                break;
            }
        }

        /// <summary>
        /// ヘルプメニューを選択した場合の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HelpMenu_Click(object sender, RoutedEventArgs e) {
            switch (((MenuItem)sender).Name) {
            case nameof(About):
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                MessageBox.Show(string.Format(Properties.Resources.I0006, version), "OverParse");
                break;
            case nameof(ResetLogFolder):
                Properties.Settings.Default.Path = "Z:\\OBVIOUSLY\\BROKEN\\DEFAULT\\PATH";
                EndEncounterNoLogImpl();
                break;
            case nameof(PluginUpdate):
                if (Properties.Settings.Default.LaunchMethod == "Tweaker") {
                    MessageBox.Show(Properties.Resources.E0006);
                    return;
                }
                EncounterLog.UpdatePlugin(Properties.Settings.Default.Path);
                EndEncounterNoLogImpl();
                break;
            case nameof(Reset):
                var result = MessageBox.Show(Properties.Resources.I0002, "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result != MessageBoxResult.Yes) {
                    return;
                }
                Console.WriteLine("Resetting");
                Properties.Settings.Default.Reset();
                Properties.Settings.Default.ResetInvoked = true;
                Properties.Settings.Default.Save();
                Process.Start(Application.ResourceAssembly.Location);
                Application.Current.Shutdown();
                break;
            }
        }

        /// <summary>
        /// リンクメニューを選択した場合の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LinkMenu_Click(object sender, RoutedEventArgs e) {
            switch (((MenuItem)sender).Name) {
            case nameof(PSOWorld):
                Process.Start("http://www.pso-world.com/forums/showthread.php?t=232386");
                break;
            case nameof(GitHub):
                Process.Start("https://github.com/TyroneSama/OverParse");
                break;
            case nameof(EQSchedule):
                Process.Start("https://calendar.google.com/calendar/embed?src=pso2emgquest@gmail.com&mode=agenda");
                break;
            case nameof(ArksLayer):
                Process.Start("http://arks-layer.com/");
                break;
            case nameof(Bumped):
                Process.Start("http://www.bumped.org/psublog/");
                break;
            case nameof(Fulldive):
                Process.Start("http://www.fulldive.nu/");
                break;
            case nameof(ShamelessPlug):
                Process.Start("http://twitch.tv/tyronesama");
                break;
            case nameof(SWiki):
                Process.Start("http://pso2.swiki.jp/");
                break;
            }
        }
    }
}