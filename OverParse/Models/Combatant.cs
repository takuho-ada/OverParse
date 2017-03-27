using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace OverParse.Models
{
    public class Combatant
    {
        public enum TemporaryEnum
        {
            IS_AIS,
            IS_ZANVERSE,
            IS_TURRET,
            NOT_TEMPORARY,
        }

        private const float maxBGopacity = 0.6f;

        public string ID { get; set; }
        public string Name { get; set; }
        public float PercentReadDPS { get; set; }
        public int ActiveTime { get; set; }
        public TemporaryEnum IsTemporary { get; set; }
        public List<Attack> Attacks { get; set; }

        Color green;

        public static string[] AISAttackIDs = new string[] {
            "119505187" , // A.I.S rifle (Solid Vulcan)
            "79965782"  , // A.I.S melee first attack (Photon Saber)
            "79965783"  , // A.I.S melee second attack (Photon Saber)
            "79965784"  , // A.I.S melee third attack (Photon Saber)
            "80047171"  , // A.I.S dash melee (Photon Saber)
            "434705298" , // A.I.S rockets (Photon Grenade)
            "79964675"  , // A.I.S gap closer PA attack (Photon Rush)
            "1460054769", // A.I.S cannon (Photon Blaster)
            "4081218683", // A.I.S mob freezing attack (Photon Blizzard)
            "3298256598", // A.I.S Weak Bullet
            "2826401717", // A.I.S Area Heal
        };
        public static string[] TurretAttakIDs = new string[] {
            "1852253343", // Normal Turret
            "1358461404", // Rodos Grapple Turret
            "2414748436", // Facility Cannon
            "1954812953", // Photon Cannon uncharged
            "2822784832", // Photon Cannon charged
            "791327364" , // Binding Arrow Turret
            "3339644659", // Photon Particle Turret
        };
        public static readonly string ZanverseID = "2106601422";

        // ダメージ種別
        public bool IsAlly => (int.Parse(ID) >= 10000000 && !IsZanverse && !IsTurret);
        public bool IsAIS => (IsTemporary == TemporaryEnum.IS_AIS);
        public bool IsZanverse => (IsTemporary == TemporaryEnum.IS_ZANVERSE);
        public bool IsTurret => (IsTemporary == TemporaryEnum.IS_TURRET);
        public bool IsNotTemporary => (IsTemporary == TemporaryEnum.NOT_TEMPORARY);
        public bool IsSeparated => (IsAIS || IsZanverse || IsTurret);
        public bool IsYou => (ID == Hacks.currentPlayerID);

        // 各種ダメージ
        public int Damage => this.Attacks.Sum(a => a.Damage);
        public int ZanverseDamage => this.Attacks.Where(a => a.ID == ZanverseID).Sum(a => a.Damage);
        public int AISDamage => this.Attacks.Where(a => AISAttackIDs.Contains(a.ID)).Sum(a => a.Damage);
        public int TurretDamage => this.Attacks.Where(a => TurretAttakIDs.Contains(a.ID)).Sum(a => a.Damage);
        public int ReadDamage => IsSeparated ? Damage : Attacks.WithoutSeparated().Sum(a => a.Damage);

        // DPS
        public float DPS => Damage / (float)ActiveTime;
        public float ReadDPS => ReadDamage / (float)ActiveTime;

        // 最大ダメージ
        public Attack MaxHitAttack => Attacks.OrderByDescending(a => a.Damage).FirstOrDefault();
        public string MaxHitID => MaxHitAttack.ID;
        public int MaxHitDamage => MaxHitAttack.Damage;

        // 割合系
        public float PercentJA => Attacks.Count(a => a.IsJA) * 100f / Attacks.Count();
        public float PercentCritical => Attacks.Count(a => a.IsCritical) * 100f / Attacks.Count();

        // フォーム表示(ユーザ名)
        public string DisplayName => (Properties.Settings.Default.AnonymizeNames && IsAlly) ? AnonymousName : Name;
        public string AnonymousName => IsYou ? Name : "--";

        // フォーム表示(その他)
        public string DisplayDamage => ReadDamage.ToString("N0");
        public string DisplayDPS => (Properties.Settings.Default.ShowRawDPS) ? FormatNumber(ReadDPS) : DisplayPercentDPS;
        public string DisplayPercentDPS => (PercentReadDPS < -0.5f) ? "--" : $"{PercentReadDPS:0.0}%";
        public string DisplayPercentJA => $"{PercentJA:0.0}%";
        public string DisplayMaxHit => (MaxHitAttack == null) ? "--" : $"{MaxHitAttack.Damage.ToString("N0")} ({MaxHitAttack.Name})";

        public Brush Brush {
            get {
                if (Properties.Settings.Default.ShowDamageGraph && (IsAlly)) {
                    return generateBarBrush(Color.FromArgb(200, 65, 112, 166), new Color());
                } else {
                    if (IsYou && Properties.Settings.Default.HighlightYourDamage)
                        return new SolidColorBrush(green);
                    return new SolidColorBrush(new Color());
                }

            }
        }

        public Brush Brush2 {
            get {
                if (Properties.Settings.Default.ShowDamageGraph && (IsAlly && !IsZanverse)) {
                    return generateBarBrush(Color.FromArgb(140, 65, 112, 166), Color.FromArgb(64, 16, 16, 16));
                } else {
                    if (IsYou && Properties.Settings.Default.HighlightYourDamage)
                        return new SolidColorBrush(green);
                    return new SolidColorBrush(Color.FromArgb(64, 16, 16, 16));
                }
            }
        }

        LinearGradientBrush generateBarBrush(Color c, Color c2) {
            if (!Properties.Settings.Default.ShowDamageGraph)
                c = new Color();

            if (IsYou && Properties.Settings.Default.HighlightYourDamage)
                c = green;

            LinearGradientBrush lgb = new LinearGradientBrush();
            lgb.StartPoint = new System.Windows.Point(0, 0);
            lgb.EndPoint = new System.Windows.Point(1, 0);
            lgb.GradientStops.Add(new GradientStop(c, 0));
            lgb.GradientStops.Add(new GradientStop(c, ReadDamage / maxShare));
            lgb.GradientStops.Add(new GradientStop(c2, ReadDamage / maxShare));
            lgb.GradientStops.Add(new GradientStop(c2, 1));
            lgb.SpreadMethod = GradientSpreadMethod.Repeat;
            return lgb;
        }

        public static float maxShare = 0;


        private String FormatNumber(float value) {
            int num = (int)Math.Round(value);

            if (value >= 100000000)
                return (value / 1000000).ToString("#,0") + "M";
            if (value >= 1000000)
                return (value / 1000000D).ToString("0.0") + "M";
            if (value >= 100000)
                return (value / 1000).ToString("#,0") + "K";
            if (value >= 1000)
                return (value / 1000D).ToString("0.0") + "K";
            return value.ToString("#,0");
        }


        public Combatant(string id, string name, TemporaryEnum temp) {
            ID = id;
            Name = name;
            Attacks = new List<Attack>();
            IsTemporary = temp;
            PercentReadDPS = 0;
            ActiveTime = 0;
            green = Color.FromArgb(160, 32, 130, 32);
        }

        public Combatant(string id, string name) : this(id, name, TemporaryEnum.NOT_TEMPORARY) { }

        public Combatant(Combatant other) : this(other.ID, other.Name, other.IsTemporary) {
            Attacks = other.Attacks.Select(a => new Attack(a)).ToList();
            PercentReadDPS = other.PercentReadDPS;
            ActiveTime = other.ActiveTime;
        }
    }
}