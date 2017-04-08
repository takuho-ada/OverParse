using OverParse.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace OverParse
{
    public class Log
    {
        private const int pluginVersion = 4;

        public bool Empty { get; private set; } = false;
        public bool Valid { get; private set; } = true;
        public bool Running { get; private set; } = false;

        public int TimestampBeg { get; private set; } = 0;
        public int TimestampEnd { get; private set; } = 0;
        public int Elapse => TimestampEnd - TimestampBeg;

        public string Filename { get; private set; }
        public List<Combatant> Combatants { get; set; } = new List<Combatant>();
        public DirectoryInfo Directory { get; private set; }

        private List<string> instances = new List<string>();
        private StreamReader LogReader { get; }

        public Log(string attemptDirectory) {

            // 環境設定
            SetupEnvironment(attemptDirectory);
            if (!Valid || Empty) {
                return;
            }

            // ログファイルの読込み準備
            var log = LatestDamagelog();
            var stream = File.Open(LatestDamagelog().FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(0, SeekOrigin.Begin);
            LogReader = new StreamReader(stream);
            Console.WriteLine($"Reading from {log.FullName}");
            Filename = log.Name;

            // ログインユーザのIDを取得
            var lines = LogReader.ReadToEnd().Split('\n').Where(l => l.Length > 0);
            var dumps = lines.Select(line => new DamageDump(line));
            foreach (var dump in dumps) {
                if (!dump.IsHeader && dump.IsCurrentPlayerIdData()) {
                    Hacks.currentPlayerID = dump.SourceID;
                    Console.WriteLine($"Found existing active player ID: {dump.SourceID}");
                }
            }
        }

        public FileInfo LatestDamagelog() {
            return Damagelogs().LastOrDefault();
        }

        public IEnumerable<FileInfo> Damagelogs() {
            if (!Directory.Exists || !Directory.GetFiles().Any()) {
                return Enumerable.Empty<FileInfo>();
            }
            var re = new Regex(@"\d+\.csv");
            return Directory.GetFiles().Where(f => re.IsMatch(f.Name)).OrderBy(f => f.Name);
        }

        public void DebugLoadLog(FileInfo file, int beg, int end) {
            var dumps = File.ReadLines(file.FullName).Where(l => l.Length > 0).Select(l => new DamageDump(l));
            if (beg != 0 || end != 0) {
                var line = 0;
                dumps = dumps.Where(dump => {
                    line += 1;
                    return dump.IsCurrentPlayerIdData() || (line >= beg && line <= end);
                });
            }
            UpdateLogInternal(dumps);
        }

        public void UpdateLog() {
            if (!Valid || Empty) {
                return;
            }
            var lines = LogReader.ReadToEnd().Split('\n');
            var dumps = lines.Where(l => l.Length > 0).Select(l => new DamageDump(l));
            UpdateLogInternal(dumps);
        }

        private void UpdateLogInternal(IEnumerable<DamageDump> dumps) {
            foreach (var dump in dumps) {
                if (dump.IsCurrentPlayerIdData()) {
                    Hacks.currentPlayerID = dump.SourceID;
                    Console.WriteLine($"Found new active player ID: {dump.SourceID}");
                    continue;
                }
                if (!instances.Contains(dump.InstanceID)) {
                    instances.Add(dump.InstanceID);
                }
                if (dump.IsInvalidDamageData()) {
                    continue;
                }
                var source = Combatants.Where(c => c.ID == dump.SourceID && c.IsDefault).FirstOrDefault();
                if (source == null) {
                    source = new Combatant(dump.SourceID, dump.SourceName);
                    Combatants.Add(source);
                }
                TimestampEnd = int.Parse(dump.Timestamp);
                if (TimestampBeg == 0) {
                    Console.WriteLine($"FIRST ATTACK RECORDED: {dump.Damage} dmg from {dump.SourceID} ({dump.SourceName}) with {dump.AttackID}, to {dump.TargetID} ({dump.TargetName})");
                    TimestampBeg = TimestampEnd;
                }
                source.Attacks.Add(new Attack(dump, Elapse));
                Running = true;
            }
            Combatants.Sort((x, y) => y.Damage.CompareTo(x.Damage));
        }

        public FileInfo WriteLog() {
            Console.WriteLine("Logging encounter information to file");

            // Debug for ID mapping
            foreach (var c in Combatants.Where(c => c.IsPlayer)) {
                foreach (var a in c.Attacks.Where(a => !a.HasName)) {
                    TimeSpan t = TimeSpan.FromSeconds(a.Elapse);
                    Console.WriteLine($"{t.ToString("dd\\.hh\\:mm\\:ss")} unmapped: {a.ID} ({a.Damage} dmg from {c.Name})");
                }
            }

            // データがないよー
            var work = Combatants.Separate().ToList();
            if (!work.Any()) {
                return null;
            }

            // タイトル
            var now = DateTime.Now;
            var log = new StringBuilder();
            log.AppendLine($"{now.ToString("F")} | {Combatant.DisplayActiveTime} | {Combatant.DisplayTotalDamage} dmg | {Combatant.DisplayTotalDPS} DPS");
            log.AppendLine();

            // 一覧(基本)
            foreach (var c in work.Select(c => new Combatant.LogBinder(c))) {
                log.AppendLine(c.NormalLine);
            }
            log.AppendLine();
            log.AppendLine();

            // 一覧(詳細)
            foreach (var c in work.Select(c => new Combatant.LogBinder(c))) {
                log.AppendLine(c.DetailHeaderLine);
                log.AppendLine();
                foreach (var line in c.AttackDetailLines()) {
                    log.AppendLine(line);
                }
                log.AppendLine();
            }

            // インスタンスID
            log.AppendLine($"Instance IDs: {String.Join(", ", instances.ToArray())}");

            // 出力
            System.IO.Directory.CreateDirectory($"Logs/{now.ToString("yyyy-MM-dd")}");
            var fileinfo = new FileInfo($"Logs/{now.ToString("yyyy-MM-dd")}/OverParse - {now.ToString("yyyy-MM-dd_HH-mm-ss")}.txt");
            File.WriteAllText(fileinfo.FullName, log.ToString());
            return fileinfo;
        }

        private void SetupEnvironment(string attemptDirectory) {
            // アカウント停止に関する警告(初回のみ)
            if (Properties.Settings.Default.BanWarning) {
                var result = MessageBox.Show(Properties.Resources.W0001, "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No) {
                    Environment.Exit(-1);
                }
                Properties.Settings.Default.BanWarning = false;
            }

            // PSO2インストールディレクトリの選択
            bool nagMe = false;
            while (!File.Exists($"{attemptDirectory}\\pso2.exe")) {
                Console.WriteLine("Invalid pso2_bin directory, prompting for new one...");

                if (nagMe) {
                    MessageBox.Show(Properties.Resources.I0007, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                } else {
                    MessageBox.Show(Properties.Resources.I0008, "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    nagMe = true;
                }

                //WINAPI FILE DIALOGS DON'T SHOW UP FOR PEOPLE SOMETIMES AND I HAVE NO IDEA WHY, *** S I C K  M E M E ***
                //VistaFolderBrowserDialog oDialog = new VistaFolderBrowserDialog();
                //oDialog.Description = "Select your pso2_bin folder...";
                //oDialog.UseDescriptionForTitle = true;
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description = "Select your pso2_bin folder. This will be inside the folder you installed PSO2 to.";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    attemptDirectory = dialog.SelectedPath;
                    Console.WriteLine($"Testing {attemptDirectory} as pso2_bin directory...");
                    Properties.Settings.Default.Path = attemptDirectory;
                } else {
                    Console.WriteLine("Canceled out of directory picker");
                    MessageBox.Show("OverParse needs a valid PSO2 installation to function.\nThe application will now close.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    Environment.Exit(-1); // ABORT ABORT ABORT
                }
            }

            // インストールディレクトリが選択された段階で有効とみなす
            Valid = true;

            // ログディレクトリの作成
            Console.WriteLine("Making sure pso2_bin\\damagelogs exists");
            Directory = new DirectoryInfo($"{attemptDirectory}\\damagelogs");

            Console.WriteLine("Checking for damagelog directory override");
            if (File.Exists($"{attemptDirectory}\\plugins\\PSO2DamageDump.cfg")) {
                Console.WriteLine("Found a config file for damage dump plugin, parsing");
                String[] lines = File.ReadAllLines($"{attemptDirectory}\\plugins\\PSO2DamageDump.cfg");
                foreach (String s in lines) {
                    String[] split = s.Split('=');
                    Console.WriteLine(split[0] + "|" + split[1]);
                    if (split.Length < 2)
                        continue;
                    if (split[0].Split('[')[0] == "directory") {
                        Directory = new DirectoryInfo(split[1]);
                        Console.WriteLine($"Log directory override: {split[1]}");
                    }
                }
            } else {
                Console.WriteLine("No PSO2DamageDump.cfg");
            }

            // 起動モードが未選択(初回起動)の場合は起動モードを選択
            if (Properties.Settings.Default.LaunchMethod == "Unknown") {
                Console.WriteLine("LaunchMethod prompt");
                var result = MessageBox.Show("Do you use the PSO2 Tweaker?", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                Properties.Settings.Default.LaunchMethod = (result == MessageBoxResult.Yes) ? "Tweaker" : "Manual";
            }

            // 起動モードによる処理(Tweaker or Manual)
            if (Properties.Settings.Default.LaunchMethod == "Tweaker") {
                bool warn = !Directory.Exists || !Directory.GetFiles().Any();
                if (warn && Hacks.DontAsk) {
                    Console.WriteLine("No damagelog warning");
                    MessageBox.Show("Your PSO2 folder doesn't contain any damagelogs. This is not an error, just a reminder!\n\nPlease turn on the Damage Parser plugin in PSO2 Tweaker (orb menu > Plugins). OverParse needs this to function. You may also want to update the plugins while you're there.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    Hacks.DontAsk = true;
                    Properties.Settings.Default.FirstRun = false;
                    Properties.Settings.Default.Save();
                    return;
                }
            } else if (Properties.Settings.Default.LaunchMethod == "Manual") {
                // インストールされているプラグインのバージョン確認
                var plugins = new string[] { "\\pso2h.dll", "\\ddraw.dll", "\\plugins\\PSO2DamageDump.dll" };
                var pluginExists = plugins.All(plugin => File.Exists($"{attemptDirectory}{plugin}"));
                if (!pluginExists) {
                    Properties.Settings.Default.InstalledPluginVersion = -1;
                }
                Console.WriteLine($"Installed: {Properties.Settings.Default.InstalledPluginVersion} / Current: {pluginVersion}");
                // プラグインの更新
                if (Properties.Settings.Default.InstalledPluginVersion < pluginVersion) {
                    var prompt = "";
                    if (pluginExists) {
                        Console.WriteLine("Prompting for plugin update");
                        prompt = "This release of OverParse includes a new version of the parsing plugin. Would you like to update now?\n\nOverParse may behave unpredictably if you use a different version than it expects.";
                    } else {
                        Console.WriteLine("Prompting for initial plugin install");
                        prompt = "OverParse needs a Tweaker plugin to recieve its damage information.\n\nThe plugin can be installed without the Tweaker, but it won't be automatically updated, and I can't provide support for this method.\n\nDo you want to try to manually install the Damage Parser plugin?";
                    }
                    switch (MessageBox.Show(prompt, "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question)) {
                    case MessageBoxResult.Yes:
                        Console.WriteLine("Accepted plugin install");
                        if (!UpdatePlugin(attemptDirectory) && !pluginExists) {
                            Environment.Exit(-1);
                        }
                        break;
                    default:
                        if (!pluginExists) {
                            Console.WriteLine("Denied plugin install");
                            MessageBox.Show("OverParse needs the Damage Parser plugin to function.\n\nThe application will now close.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                            Environment.Exit(-1);
                        }
                        break;
                    }
                }
            }

            // 初回起動フラグをオフ
            Properties.Settings.Default.FirstRun = false;

            // ログファイルの存在確認
            Empty = !Directory.Exists || !Directory.GetFiles().Any();
        }

        public bool UpdatePlugin(string attemptDirectory) {
            try {
                File.Copy(System.IO.Directory.GetCurrentDirectory() + "\\resources\\pso2h.dll", attemptDirectory + "\\pso2h.dll", true);
                File.Copy(System.IO.Directory.GetCurrentDirectory() + "\\resources\\ddraw.dll", attemptDirectory + "\\ddraw.dll", true);
                System.IO.Directory.CreateDirectory(attemptDirectory + "\\plugins");
                File.Copy(System.IO.Directory.GetCurrentDirectory() + "\\resources\\PSO2DamageDump.dll", attemptDirectory + "\\plugins" + "\\PSO2DamageDump.dll", true);
                Properties.Settings.Default.InstalledPluginVersion = pluginVersion;
                MessageBox.Show("Setup complete! A few files have been copied to your pso2_bin folder.\n\nIf PSO2 is running right now, you'll need to close it before the changes can take effect.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                Console.WriteLine("Plugin install successful");
                return true;
            } catch (Exception ex) {
                MessageBox.Show("Something went wrong with manual installation. This usually means that the files are already in use: try again with PSO2 closed.\n\nIf you've recieved this message even after closing PSO2, you may need to run OverParse as administrator.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"PLUGIN INSTALL FAILED: {ex.ToString()}");

                return false;
            }
        }
    }
}
