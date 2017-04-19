using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace OverParse.Models
{
    public class Combatant
    {
        public enum TypeEnum
        {
            IS_AIS,
            IS_ZANVERSE,
            IS_TURRET,
            IS_DEFAULT,
        }

        public string ID { get; private set; }
        public string Name { get; private set; }
        public TypeEnum Type { get; private set; }
        public List<Attack> Attacks { get; set; }

        public static int ActiveTime { get; private set; } = 0;
        public static int TotalDamage { get; private set; } = 0;
        public static float MaxShare { get; private set; } = 0;
        private static Dictionary<string, string> users = new Dictionary<string, string>();

        public override string ToString() {
            return $"{base.ToString()} - ID: {ID}, Name: {Name}, Type: {Type}, Attacks: {Attacks.Count}";
        }

        public static void Update(IEnumerable<Combatant> combatants) {
            var work = combatants.Where(c => c.IsAlly).ToList();
            ActiveTime = work.ActiveTime();
            TotalDamage = work.TotalDamage();
            MaxShare = work.MaxShare();
        }

        public Combatant(string id, string name, TypeEnum temp) {
            ID = id;
            Name = name;
            Attacks = new List<Attack>();
            Type = temp;
        }

        public Combatant(string id, string name) : this(id, name, TypeEnum.IS_DEFAULT) { }

        public Combatant(Combatant other) : this(other.ID, other.Name, other.Type) {
            Attacks = other.Attacks.ToList();
        }

        public IEnumerable<Tuple<string, IEnumerable<Attack>>> AttackDetails() {
            if (Properties.Settings.Default.SeparateZanverse && IsZanverse) {
                var users = Attacks.Select(a => new { id = a.UserID, name = a.UserName }).Distinct();
                foreach (var user in users) {
                    var attacks = Attacks.Where(a => a.UserID == user.id);
                    yield return Tuple.Create(user.name, attacks);
                }
            } else {
                var names = Attacks.Select(a => a.NameOrId).Distinct();
                foreach (var name in names) {
                    var attacks = Attacks.Where(a => a.NameOrId == name);
                    yield return Tuple.Create(name, attacks);
                }
            }
        }

        // (全体)
        public static float TotalDPS => TotalDamage / (float)ActiveTime;
        public static string DisplayActiveTime => ActiveTime > 3600 ? $"{TimeSpan.FromSeconds(ActiveTime):h\\:mm\\:ss}" : $"{TimeSpan.FromSeconds(ActiveTime):mm\\:ss}";
        public static string DisplayTotalDamage => $"{TotalDamage:#,0}";
        public static string DisplayTotalDPS => $"{TotalDPS:#,0.00}";

        // ダメージ種別
        public bool IsAlly => int.Parse(ID) >= 10000000;
        public bool IsPlayer => (IsAlly && !IsZanverse && !IsTurret);
        public bool IsAIS => (Type == TypeEnum.IS_AIS);
        public bool IsZanverse => (Type == TypeEnum.IS_ZANVERSE);
        public bool IsTurret => (Type == TypeEnum.IS_TURRET);
        public bool IsDefault => (Type == TypeEnum.IS_DEFAULT);
        public bool IsSeparated => (IsAIS || IsZanverse || IsTurret);
        public bool IsYou => (ID == Hacks.currentPlayerID);

        // 各種ダメージ
        public int Damage => this.Attacks.Sum(a => a.Damage);
        public int ZanverseDamage => this.Attacks.Where(a => a.IsZanverse).Sum(a => a.Damage);
        public int AISDamage => this.Attacks.Where(a => a.IsAIS).Sum(a => a.Damage);
        public int TurretDamage => this.Attacks.Where(a => a.IsTurret).Sum(a => a.Damage);

        // DPS
        public float DPS => Damage / (float)ActiveTime;
        public float Contribute => IsAlly ? (Damage / (float)TotalDamage) : -1;

        // 最大ダメージ
        public Attack MaxHitAttack => Attacks.OrderByDescending(a => a.Damage).FirstOrDefault();
        public string MaxHitID => MaxHitAttack.ID;
        public int MaxHitDamage => MaxHitAttack.Damage;

        public class BaseBinder
        {
            protected Combatant c;

            public BaseBinder(Combatant c) {
                this.c = c;
            }

            public virtual string Name => c.Name;
            public virtual string Damage => $"{c.Damage:#,0}";
            public virtual string Contribute => (c.Contribute < 0) ? "--" : $"{c.Contribute:0.0%}";
            public virtual string DPS => $"{c.DPS:#,0.00}";
            public virtual string MaxHit => (c.MaxHitAttack == null) ? "--" : $"{c.MaxHitAttack.Damage:#,0} ({c.MaxHitAttack.Name})";
            public virtual string JA => $"{c.Attacks.PercentJA():0.0%}";
        }

        public class LogBinder : BaseBinder
        {
            public LogBinder(Combatant c) : base(c) { }

            public string NormalLine => $"{Name} | {Damage} dmg | {Contribute} contrib | {DPS} DPS | JA: {JA} | Max: {MaxHit}";
            public string DetailHeaderLine => $"###### {Name} - {Damage} dmg ({Contribute}) ######";

            public IEnumerable<string> AttackDetailLines() {
                var values = c.AttackDetails().OrderByDescending(x => x.Item2.Sum(a => a.Damage));
                foreach (var value in values) {
                    var name = value.Item1;
                    var attacks = value.Item2;

                    var percent = $"{attacks.Sum(a => a.Damage) * 100d / c.Damage:00.00}".Substring(0, 5);
                    var hits = $"{attacks.Count():#,0}";
                    var sum = $"{attacks.Sum(a => a.Damage):#,0}";
                    var min = $"{attacks.Min(a => a.Damage):#,0}";
                    var max = $"{attacks.Max(a => a.Damage):#,0}";
                    var avg = $"{attacks.Average(a => a.Damage):#,0}";
                    var ja = $"{attacks.PercentJA():0.0%}";

                    yield return $"{percent}% | {name} ({sum} dmg)";
                    yield return $"       |   {hits} hits - {min} min, {avg} avg, {max} max, {ja} ja";
                }
            }
        }

        public class FormBinder : BaseBinder
        {
            private static Color green = Color.FromArgb(160, 32, 130, 32);
            private static Color color1fg = Color.FromArgb(200, 65, 112, 166);
            private static Color color1bg = new Color();
            private static Color color2fg = Color.FromArgb(140, 65, 112, 166);
            private static Color color2bg = Color.FromArgb(64, 16, 16, 16);

            public FormBinder(Combatant c) : base(c) { }

            public Brush Brush1 => generateBarBrush(color1fg, color1bg);
            public Brush Brush2 => generateBarBrush(color2fg, color2bg);
            public override string Name => (Properties.Settings.Default.AnonymizeNames && c.IsPlayer && !c.IsYou) ? "--" : base.Name;
            public override string DPS => FormatUnit(c.DPS);
            public string ContributeOrDPS => (Properties.Settings.Default.ShowRawDPS) ? DPS : Contribute;

            private Brush generateBarBrush(Color fgColor, Color bgColor) {
                bool showDamageGraph = (Properties.Settings.Default.ShowDamageGraph && c.IsPlayer);
                bool highlightYou = (Properties.Settings.Default.HighlightYourDamage && c.IsYou);
                if (showDamageGraph) {
                    // グラフ表示
                    if (highlightYou) {
                        fgColor = green;
                    }
                    LinearGradientBrush lgb = new LinearGradientBrush();
                    lgb.StartPoint = new System.Windows.Point(0, 0);
                    lgb.EndPoint = new System.Windows.Point(1, 0);
                    lgb.GradientStops.Add(new GradientStop(fgColor, 0));
                    lgb.GradientStops.Add(new GradientStop(fgColor, c.Damage / Combatant.MaxShare));
                    lgb.GradientStops.Add(new GradientStop(bgColor, c.Damage / Combatant.MaxShare));
                    lgb.GradientStops.Add(new GradientStop(bgColor, 1));
                    lgb.SpreadMethod = GradientSpreadMethod.Repeat;
                    return lgb;
                } else if (highlightYou) {
                    // 自分(別色)
                    return new SolidColorBrush(green);
                } else {
                    // その他(背景色のみ)
                    return new SolidColorBrush(bgColor);
                }
            }

            private String FormatUnit(float value) {
                if (value >= 1000000f) {
                    return $"{value / 1000000f:#,0.0}M";
                } else if (value >= 1000f) {
                    return $"{value / 1000f:#,0.0}K";
                } else {
                    return $"{value:#,0}";
                }
            }
        }
    }

    public static class CombatantExtentions
    {
        public static int ActiveTime(this IEnumerable<Combatant> combatants) {
            if (combatants.Any()) {
                return combatants.Max(c => {
                    if (c.Attacks.Any()) {
                        return c.Attacks.Max(a => a.Elapse);
                    } else {
                        return 0;
                    }
                });
            } else {
                return 0;
            }
        }

        public static int TotalDamage(this IEnumerable<Combatant> combatants) {
            return combatants.Sum(c => c.Damage);
        }

        public static int MaxShare(this IEnumerable<Combatant> combatants) {
            if (combatants.Any()) {
            return combatants.Max(c => c.IsPlayer ? c.Damage : 0);
            } else {
                return 0;
            }
        }

        public static IEnumerable<Combatant> Separate(this IEnumerable<Combatant> combatants) {
            return combatants.WithoutSeparated()
                .Concat(combatants.AISCombatants())
                .OrderByDescending(c => c.Damage)
                .Concat(combatants.TurretCombatants())
                .Concat(combatants.ZanverseCombatants())
                .Concat(combatants.EnemyCombatants().OrderBy(c => c.ID));
        }

        private static IEnumerable<Combatant> WithoutSeparated(this IEnumerable<Combatant> combatants) {
            if (Properties.Settings.Default.SeparateAIS && Properties.Settings.Default.HidePlayers) {
                yield break;
            }
            foreach (var c in combatants.Where(c => c.IsPlayer)) {
                var result = new Combatant(c.ID, c.Name, c.Type);
                result.Attacks = c.Attacks.WithoutSeparated().ToList();
                yield return result;
            }
        }

        private static IEnumerable<Combatant> EnemyCombatants(this IEnumerable<Combatant> combatants) {
            if (Properties.Settings.Default.HideEnemies) {
                yield break;
            }
            foreach (var c in combatants.Where(c => !c.IsAlly)) {
                yield return c;
            }
        }

        private static IEnumerable<Combatant> AISCombatants(this IEnumerable<Combatant> combatants) {
            if (!Properties.Settings.Default.SeparateAIS || Properties.Settings.Default.HideAIS) {
                yield break;
            }
            foreach (var c in combatants.Where(c => c.IsPlayer && c.AISDamage > 0)) {
                var result = new Combatant(c.ID, $"AIS|{c.Name}", Combatant.TypeEnum.IS_AIS);
                result.Attacks = c.Attacks.Where(a => a.IsAIS).ToList();
                yield return result;
            }
        }

        private static IEnumerable<Combatant> TurretCombatants(this IEnumerable<Combatant> combatants) {
            if (!Properties.Settings.Default.SeparateTurret) {
                yield break;
            }
            var result = new Combatant("99999998", "Turret", Combatant.TypeEnum.IS_TURRET);
            foreach (var c in combatants.Where(c => c.IsPlayer)) {
                result.Attacks.AddRange(c.Attacks.Where(a => a.IsTurret));
            }
            if (result.TurretDamage > 0) {
                yield return result;
            }
        }

        private static IEnumerable<Combatant> ZanverseCombatants(this IEnumerable<Combatant> combatants) {
            if (!Properties.Settings.Default.SeparateZanverse) {
                yield break;
            }
            var result = new Combatant("99999999", "Zanverse", Combatant.TypeEnum.IS_ZANVERSE);
            foreach (var c in combatants.Where(c => c.IsPlayer)) {
                result.Attacks.AddRange(c.Attacks.Where(a => a.IsZanverse));
            }
            if (result.ZanverseDamage > 0) {
                yield return result;
            }
        }
    }
}
