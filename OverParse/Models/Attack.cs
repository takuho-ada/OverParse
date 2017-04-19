using System.Collections.Generic;
using System.Linq;

namespace OverParse.Models
{
    public class Attack
    {
        private static readonly string[] AISAttackIDs = new string[] {
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
        private static readonly string[] TurretAttakIDs = new string[] {
            "1852253343", // Normal Turret
            "1358461404", // Rodos Grapple Turret
            "2414748436", // Facility Cannon
            "1954812953", // Photon Cannon uncharged
            "2822784832", // Photon Cannon charged
            "791327364" , // Binding Arrow Turret
            "3339644659", // Photon Particle Turret
        };
        private static readonly string ZanverseID = "2106601422";
        private static readonly string PetDamageID = "3460765776";
        private static readonly SkillDictionary dic = SkillDictionary.GetInstance();

        public string ID => dump.AttackID;
        public string UserID => dump.SourceID;
        public string UserName => dump.SourceName;
        public int Damage => dump.Damage;
        public int Elapse { get; private set; }
        public bool IsJA => dump.IsJA;
        public bool IsCritical => dump.IsCritical;

        private DamageDump dump;

        public override string ToString() {
            return $"{base.ToString()} - ID: {ID}, UserID: {UserID}, UserName: {UserName}, Damage: {Damage}, Elapse: {Elapse}, IsJA: {IsJA}, IsCritical: {IsCritical}";
        }

        public Attack(DamageDump dump, int elapse) {
            this.dump = dump;
            this.Elapse = elapse;
        }

        public bool HasName => dic.ContainsKey(ID);
        public string Name => dic.Find(ID);
        public string NameOrId => dic.Find(ID, ID);

        public bool IsAIS => AISAttackIDs.Contains(ID);
        public bool IsTurret => TurretAttakIDs.Contains(ID);
        public bool IsZanverse => ZanverseID == ID;
    }

    public static class AttackExtenstions
    {
        public static float PercentJA(this IEnumerable<Attack> attacks) {
            return attacks.Count(a => a.IsJA) / (float)attacks.Count();
        }

        public static IEnumerable<Attack> WithoutSeparated(this IEnumerable<Attack> attacks) {
            return attacks.WithoutZanverse().WithoutTurret().WithoutAIS();
        }

        private static IEnumerable<Attack> WithoutZanverse(this IEnumerable<Attack> attacks) {
            if (Properties.Settings.Default.SeparateZanverse) {
                return attacks.Where(a => !a.IsZanverse);
            } else {
                return attacks;
            }
        }

        private static IEnumerable<Attack> WithoutTurret(this IEnumerable<Attack> attacks) {
            if (Properties.Settings.Default.SeparateTurret) {
                return attacks.Where(a => !a.IsTurret);
            } else {
                return attacks;
            }
        }

        private static IEnumerable<Attack> WithoutAIS(this IEnumerable<Attack> attacks) {
            if (Properties.Settings.Default.SeparateAIS) {
                return attacks.Where(a => !a.IsAIS);
            } else {
                return attacks;
            }
        }
    }
}
