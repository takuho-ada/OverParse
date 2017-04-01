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

        public bool notEmpty;
        public bool valid;
        public bool running;
        private int startTimestamp = 0;
        public int newTimestamp = 0;
        public string filename;
        private string encounterData;
        private List<string> instances = new List<string>();
        private StreamReader logReader;
        public List<Combatant> combatants = new List<Combatant>();
        private Random random = new Random();
        public DirectoryInfo logDirectory;
        public List<Combatant> backupCombatants = new List<Combatant>();

        public Log(string attemptDirectory) {
            valid = false;
            notEmpty = false;
            running = false;

            bool nagMe = false;

            if (Properties.Settings.Default.BanWarning) {
                MessageBoxResult panicResult = MessageBox.Show(Properties.Resources.W0001, "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (panicResult == MessageBoxResult.No)
                    Environment.Exit(-1);
                Properties.Settings.Default.BanWarning = false;
            }

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

                System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description = "Select your pso2_bin folder. This will be inside the folder you installed PSO2 to.";
                System.Windows.Forms.DialogResult picked = dialog.ShowDialog();
                if (picked == System.Windows.Forms.DialogResult.OK) {
                    attemptDirectory = dialog.SelectedPath;
                    Console.WriteLine($"Testing {attemptDirectory} as pso2_bin directory...");
                    Properties.Settings.Default.Path = attemptDirectory;
                } else {
                    Console.WriteLine("Canceled out of directory picker");
                    MessageBox.Show("OverParse needs a valid PSO2 installation to function.\nThe application will now close.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    Environment.Exit(-1); // ABORT ABORT ABORT
                    break;
                }
            }

            if (!File.Exists($"{attemptDirectory}\\pso2.exe")) { return; }

            valid = true;

            Console.WriteLine("Making sure pso2_bin\\damagelogs exists");
            logDirectory = new DirectoryInfo($"{attemptDirectory}\\damagelogs");

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
                        logDirectory = new DirectoryInfo(split[1]);
                        Console.WriteLine($"Log directory override: {split[1]}");
                    }
                }
            } else {
                Console.WriteLine("No PSO2DamageDump.cfg");
            }

            if (Properties.Settings.Default.LaunchMethod == "Unknown") {
                Console.WriteLine("LaunchMethod prompt");
                MessageBoxResult tweakerResult = MessageBox.Show("Do you use the PSO2 Tweaker?", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                Properties.Settings.Default.LaunchMethod = (tweakerResult == MessageBoxResult.Yes) ? "Tweaker" : "Manual";
            }

            if (Properties.Settings.Default.LaunchMethod == "Tweaker") {
                bool warn = true;
                if (logDirectory.Exists) {
                    if (logDirectory.GetFiles().Count() > 0) {
                        warn = false;
                    }
                }

                if (warn && Hacks.DontAsk) {
                    Console.WriteLine("No damagelog warning");
                    MessageBox.Show("Your PSO2 folder doesn't contain any damagelogs. This is not an error, just a reminder!\n\nPlease turn on the Damage Parser plugin in PSO2 Tweaker (orb menu > Plugins). OverParse needs this to function. You may also want to update the plugins while you're there.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    Hacks.DontAsk = true;
                    Properties.Settings.Default.FirstRun = false;
                    Properties.Settings.Default.Save();
                    return;
                }
            } else if (Properties.Settings.Default.LaunchMethod == "Manual") {
                bool pluginsExist = File.Exists(attemptDirectory + "\\pso2h.dll") && File.Exists(attemptDirectory + "\\ddraw.dll") && File.Exists(attemptDirectory + "\\plugins" + "\\PSO2DamageDump.dll");
                if (!pluginsExist)
                    Properties.Settings.Default.InstalledPluginVersion = -1;

                Console.WriteLine($"Installed: {Properties.Settings.Default.InstalledPluginVersion} / Current: {pluginVersion}");

                if (Properties.Settings.Default.InstalledPluginVersion < pluginVersion) {
                    MessageBoxResult selfdestructResult;

                    if (pluginsExist) {
                        Console.WriteLine("Prompting for plugin update");
                        selfdestructResult = MessageBox.Show("This release of OverParse includes a new version of the parsing plugin. Would you like to update now?\n\nOverParse may behave unpredictably if you use a different version than it expects.", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    } else {
                        Console.WriteLine("Prompting for initial plugin install");
                        selfdestructResult = MessageBox.Show("OverParse needs a Tweaker plugin to recieve its damage information.\n\nThe plugin can be installed without the Tweaker, but it won't be automatically updated, and I can't provide support for this method.\n\nDo you want to try to manually install the Damage Parser plugin?", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    }

                    if (selfdestructResult == MessageBoxResult.No && !pluginsExist) {
                        Console.WriteLine("Denied plugin install");
                        MessageBox.Show("OverParse needs the Damage Parser plugin to function.\n\nThe application will now close.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                        Environment.Exit(-1);
                        return;
                    } else if (selfdestructResult == MessageBoxResult.Yes) {
                        Console.WriteLine("Accepted plugin install");
                        bool success = UpdatePlugin(attemptDirectory);
                        if (!pluginsExist && !success)
                            Environment.Exit(-1);
                    }
                }
            }

            Properties.Settings.Default.FirstRun = false;

            if (!logDirectory.Exists)
                return;
            if (logDirectory.GetFiles().Count() == 0)
                return;

            notEmpty = true;

            FileInfo log = logDirectory.GetFiles().Where(f => Regex.IsMatch(f.Name, @"\d+\.csv")).OrderByDescending(f => f.Name).First();
            Console.WriteLine($"Reading from {log.DirectoryName}\\{log.Name}");
            filename = log.Name;
            FileStream fileStream = File.Open(log.DirectoryName + "\\" + log.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileStream.Seek(0, SeekOrigin.Begin);
            logReader = new StreamReader(fileStream);

            // gotta get the dummy line for current player name
            foreach (var line in logReader.ReadToEnd().Split('\n').Where(l => l.Length > 0)) {
                var dump = new DamageDump(line);
                if (!dump.IsHeader && dump.IsCurrentPlayerIdData()) {
                    Hacks.currentPlayerID = dump.SourceID;
                    Console.WriteLine($"Found existing active player ID: {dump.SourceID}");
                }
            }
        }

        public bool UpdatePlugin(string attemptDirectory) {
            try {
                File.Copy(Directory.GetCurrentDirectory() + "\\resources\\pso2h.dll", attemptDirectory + "\\pso2h.dll", true);
                File.Copy(Directory.GetCurrentDirectory() + "\\resources\\ddraw.dll", attemptDirectory + "\\ddraw.dll", true);
                Directory.CreateDirectory(attemptDirectory + "\\plugins");
                File.Copy(Directory.GetCurrentDirectory() + "\\resources\\PSO2DamageDump.dll", attemptDirectory + "\\plugins" + "\\PSO2DamageDump.dll", true);
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

        public void WriteClipboard() {
            Func<int, string> FormatNumber = (int value) => {
                if (value >= 100000000) {
                    return (value / 1000000).ToString("#,0") + "M";
                } else if (value >= 1000000) {
                    return (value / 1000000D).ToString("0.#") + "M";
                } else if (value >= 100000) {
                    return (value / 1000).ToString("#,0") + "K";
                } else if (value >= 1000) {
                    return (value / 1000D).ToString("0.#") + "K";
                } else {
                    return value.ToString("#,0");
                }
            };
            var log = new StringBuilder();
            foreach (Combatant c in combatants.Where(c => c.IsAlly)) {
                var shortname = c.Name.Substring(0, 4);
                log.AppendLine($"{shortname} {FormatNumber(c.Damage)}");
            }
            if (log.Length == 0) {
                return;
            }
            try {
                Clipboard.SetText(log.ToString());
            } catch {
                //LMAO
            }
        }

        public string WriteLog() {
            Console.WriteLine("Logging encounter information to file");

            // Debug for ID mapping
            foreach (var c in combatants.Where(c => c.IsAlly)) {
                foreach (Attack a in c.Attacks.Where(a => !a.hasName)) {
                    TimeSpan t = TimeSpan.FromSeconds(a.Timestamp);
                    Console.WriteLine($"{t.ToString(@"dd\.hh\:mm\:ss")} unmapped: {a.ID} ({a.Damage} dmg from {c.Name})");
                }
            }

            // データがないよー
            if (!combatants.Any()) {
                return null;
            }

            //
            var now = DateTime.Now;
            var log = new StringBuilder();
            var timespan = TimeSpan.FromSeconds(newTimestamp - startTimestamp);
            log.AppendLine($"{now.ToString("F")} | {timespan.ToString(@"mm\:ss")}");
            log.AppendLine();

            // 一覧(基本)
            foreach (Combatant c in combatants.Where(c => c.IsAlly || c.IsZanverse || c.IsTurret)) {
                log.AppendLine($"{c.Name} | {c.DisplayDamage} dmg | {c.DisplayPercentDPS} contrib | {c.ReadDPS:#,0.0} DPS | Max: {c.DisplayMaxHit} | JA: {c.DisplayPercentJA}");
            }
            log.AppendLine();
            log.AppendLine();

            // 一覧(詳細)
            foreach (Combatant c in combatants.Where(c => c.IsAlly || c.IsZanverse || c.IsTurret)) {
                log.AppendLine($"###### {c.Name} - {c.ReadDamage.ToString("N0")} dmg ({c.DisplayPercentDPS}) ######");
                log.AppendLine();

                var attackData = new List<Tuple<string, IEnumerable<Attack>>>();

                if (c.IsZanverse && Properties.Settings.Default.SeparateZanverse) {
                    var userIds = backupCombatants.Where(c2 => c2.ZanverseDamage > 0).Select(c2 => c2.ID);
                    foreach (var userId in userIds) {
                        var target = backupCombatants.First(x => x.ID == userId);
                        var attacks = target.Attacks.Where(a => a.ID == Combatant.ZanverseID);
                        attackData.Add(Tuple.Create(target.Name, attacks));
                    }
                } else {
                    var attackNames = c.Attacks.WithoutZanverse().Select(a => a.NameOrId).Distinct();
                    foreach (var attackName in attackNames) {
                        var matchingAttacks = c.Attacks.Where(a => a.NameOrId == attackName);
                        attackData.Add(Tuple.Create(attackName, matchingAttacks));
                    }
                }

                foreach (var i in attackData.OrderByDescending(x => x.Item2.Sum(a => a.Damage))) {
                    var attacks = i.Item2;
                    var percent = attacks.Sum(a => a.Damage) * 100d / c.ReadDamage;

                    var paddedPercent = percent.ToString("00.00").Substring(0, 5);
                    var hits = attacks.Count().ToString("N0");
                    var sum = attacks.Sum(a => a.Damage).ToString("N0");
                    var min = attacks.Min(a => a.Damage).ToString("N0");
                    var max = attacks.Max(a => a.Damage).ToString("N0");
                    var avg = attacks.Average(a => a.Damage).ToString("N0");
                    var ja = string.Format("{0:0.0}", attacks.Count(a => a.IsJA) * 100d / attacks.Count()) + "%";

                    log.AppendLine($"{paddedPercent}% | {i.Item1} ({sum} dmg)");
                    log.AppendLine($"       |   {hits} hits - {min} min, {avg} avg, {max} max, {ja} ja");
                }

                log.AppendLine();
            }

            // インスタンスID
            log.AppendLine($"Instance IDs: {String.Join(", ", instances.ToArray())}");

            // 出力
            Directory.CreateDirectory($"Logs/{now.ToString("yyyy-MM-dd")}");
            File.WriteAllText($"Logs/{now.ToString("yyyy-MM-dd")}/OverParse - {now.ToString("yyyy-MM-dd_HH-mm-ss")}.txt", log.ToString());
            return filename;
        }

        public string logStatus() {
            if (!valid) {
                return "USER SHOULD PROBABLY NEVER SEE THIS";
            }

            if (!notEmpty) {
                return "No logs: Enable plugin and check pso2_bin!";
            }

            if (!running) {
                return $"Waiting for combat data...";
            }

            return encounterData;
        }

        public void GenerateFakeEntries() {
            for (int i = 0; i <= 12; i++) {
                Combatant temp = new Combatant("1000000" + i.ToString(), "TestPlayer_" + random.Next(0, 99).ToString());
                combatants.Add(temp);
            }

            for (int i = 0; i <= 9; i++) {
                Combatant temp = new Combatant(i.ToString(), "TestEnemy_" + i.ToString());
                combatants.Add(temp);
            }

            combatants.Sort((x, y) => y.DPS.CompareTo(x.DPS));

            valid = true;
            running = true;
        }

        public void UpdateLog(object sender, EventArgs e) {
            if (!valid || !notEmpty) {
                return;
            }

            var lines = logReader.ReadToEnd().Split('\n');
            var dumps = lines.Where(l => l.Length > 0).Select(l => new DamageDump(l));

            foreach (var dump in dumps) {
                // プレイヤーIDの取得
                if (dump.IsCurrentPlayerIdData()) {
                    Hacks.currentPlayerID = dump.SourceID;
                    Console.WriteLine($"Found new active player ID: {dump.SourceID}");
                    continue;
                }

                //
                if (!instances.Contains(dump.InstanceID)) {
                    instances.Add(dump.InstanceID);
                }

                // 無効なダメージ(0以下等)を除外
                if (dump.IsInvalidDamageData()) {
                    continue;
                }

                //
                var source = combatants.Where(x => x.ID == dump.SourceID && x.IsNotTemporary).FirstOrDefault();
                if (source == null) {
                    source = new Combatant(dump.SourceID, dump.SourceName);
                    combatants.Add(source);
                }

                //
                newTimestamp = int.Parse(dump.Timestamp);
                if (startTimestamp == 0) {
                    Console.WriteLine($"FIRST ATTACK RECORDED: {dump.Damage} dmg from {dump.SourceID} ({dump.SourceName}) with {dump.AttackID}, to {dump.TargetID} ({dump.TargetName})");
                    startTimestamp = newTimestamp;
                }

                //
                source.Attacks.Add(new Attack(dump.AttackID, dump.Damage, newTimestamp - startTimestamp, dump.IsJA, dump.IsCritical));
                running = true;
            }
            combatants.Sort((x, y) => y.ReadDamage.CompareTo(x.ReadDamage));

            if (newTimestamp == startTimestamp) {
                encounterData = "00:00 - ∞ DPS";
            } else {
                foreach (Combatant c in combatants.Where(c => c.IsAlly || c.IsZanverse || c.IsTurret)) {
                    c.ActiveTime = (newTimestamp - startTimestamp);
                }
            }
        }
    }
}
