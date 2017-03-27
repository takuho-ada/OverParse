using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OverParse
{
    public class Combatant
    {
        private const float maxBGopacity = 0.6f;
        public string ID;
        public string Name { get; set; }
        public float PercentDPS;
        public float PercentReadDPS;
        public int ActiveTime;
        public string isTemporary;
        public List<Attack> Attacks;
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

        public int Damage
        {
            get
            {
                return this.Attacks.Sum(x => x.Damage);
            }
        }

        public int ZanverseDamage
        {
            get
            {
                return this.Attacks.Where(a => a.ID == ZanverseID).Sum(x => x.Damage);
            }
        }

        public int AISDamage
        {
            get
            {
                return this.Attacks.Where(a => AISAttackIDs.Contains(a.ID)).Sum(x => x.Damage);
            }
        }

        public int TurretDamage
        {
            get
            {
                return this.Attacks.Where(a => TurretAttakIDs.Contains(a.ID)).Sum(x => x.Damage);
            }
        }

        public float DPS
        {
            get
            {
                return Damage / (float)ActiveTime;
            }
        }

        public float ReadDPS
        {
            get
            {
                return ReadDamage / (float)ActiveTime;
            }
        }

        public bool isAIS
        {
            get
            {
                return (isTemporary == "AIS");
            }
        }

        public bool isZanverse
        {
            get
            {
                return (isTemporary == "Zanverse");
            }
        }

        public bool isTurret
        {
            get
            {
                return (isTemporary == "Turret");
            }
        }

        public bool isYou
        {
            get
            {
                return (ID == Hacks.currentPlayerID);
            }
        }

        public int MaxHitNum
        {
            get
            {
                return MaxHitAttack.Damage;
            }
        }

        public string MaxHitID
        {
            get
            {
                return MaxHitAttack.ID;
            }
        }

        public int ReadDamage
        {
            get
            {
                if (this.isZanverse || this.isAIS || this.isTurret)
                    return Damage;

                int temp = Damage;
                if (Properties.Settings.Default.SeparateZanverse)
                    temp -= ZanverseDamage;
                if (Properties.Settings.Default.SeparateAIS)
                    temp -= AISDamage;
                if (Properties.Settings.Default.SeparateTurret)
                    temp -= TurretDamage;
                return temp;
            }
        }

        public string AnonymousName()
        {
            if (isYou)
                return Name;
            else
                return "--";
        }

        public string DisplayName
        {
            get
            {
                if (Properties.Settings.Default.AnonymizeNames && isAlly)
                    return AnonymousName();
                return Name;
            }

        }

        public Brush Brush
        {
            get
            {
                if (Properties.Settings.Default.ShowDamageGraph && (isAlly))
                {
                    return generateBarBrush(Color.FromArgb(200, 65, 112, 166), new Color());
                }
                else
                {
                    if (isYou && Properties.Settings.Default.HighlightYourDamage)
                        return new SolidColorBrush(green);
                    return new SolidColorBrush(new Color());
                }

            }
        }

        public Brush Brush2
        {
            get
            {
                if (Properties.Settings.Default.ShowDamageGraph && (isAlly && !isZanverse))
                {
                    return generateBarBrush(Color.FromArgb(140, 65, 112, 166), Color.FromArgb(64, 16, 16, 16));
                }
                else
                {
                    if (isYou && Properties.Settings.Default.HighlightYourDamage)
                        return new SolidColorBrush(green);
                    return new SolidColorBrush(Color.FromArgb(64, 16, 16, 16));
                }
            }
        }

        LinearGradientBrush generateBarBrush(Color c, Color c2)
        {
            if (!Properties.Settings.Default.ShowDamageGraph)
                c = new Color();

            if (isYou && Properties.Settings.Default.HighlightYourDamage)
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

        public bool isAlly
        {
            get
            {
                if (int.Parse(ID) >= 10000000 && !isZanverse)
                    return true;
                return false;
            }
        }

        public Attack MaxHitAttack
        {
            get
            {
                Attacks.Sort((x, y) => y.Damage.CompareTo(x.Damage));
                return Attacks.FirstOrDefault();
            }
        }

        public string MaxHit
        {
            get
            {
                if (MaxHitAttack == null)
                    return "--";

                string attack = "Unknown";
                if (MainWindow.skillDict.ContainsKey(MaxHitID))
                {
                    attack = MainWindow.skillDict[MaxHitID];
                }

                return MaxHitAttack.Damage.ToString("N0") + $" ({attack})";
            }
        }

        public string DPSReadout
        {
            get
            {
                if (Properties.Settings.Default.ShowRawDPS)
                {
                    return FormatNumber(ReadDPS);
                }
                else
                {
                    return PercentReadDPSReadout;
                }

            }
        }

        public string PercentReadDPSReadout
        {
            get
            {
                if (PercentReadDPS < -.5)
                {
                    return "--";
                }
                else
                {
                    return string.Format("{0:0.0}", PercentReadDPS) + "%";
                }
            }
        }

        public string PercentJAReadout
        {
            get
            {
                var ja = (Attacks.Count(a => a.IsJA) * 100) / (float)Attacks.Count();
                return string.Format("{0:0.0}", ja) + "%";
            }
        }

        public string PercentCriticalReadout
        {
            get
            {
                var critical = (Attacks.Count(a => a.IsCritical) * 100) / (float)Attacks.Count();
                return string.Format("{0:0.0}", critical) + "%";
            }
        }

        private String FormatNumber(float value)
        {
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

        public string DamageReadout
        {
            get
            {
                return ReadDamage.ToString("N0");
            }
        }

        public Combatant(string id, string name)
        {
            ID = id;
            Name = name;
            PercentDPS = -1;
            Attacks = new List<Attack>();
            isTemporary = "no";
            PercentReadDPS = 0;
            ActiveTime = 0;
            green = Color.FromArgb(160, 32, 130, 32);
        }

        public Combatant(string id, string name, string temp)
        {
            ID = id;
            Name = name;
            PercentDPS = -1;
            Attacks = new List<Attack>();
            isTemporary = temp;
            PercentReadDPS = 0;
            ActiveTime = 0;
            green = Color.FromArgb(160, 32, 130, 32);
        }

    }
}