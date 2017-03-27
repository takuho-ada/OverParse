using System;
using System.Collections.Generic;
using System.Linq;

namespace OverParse.Models
{
    public class Attack
    {
        public string ID;
        public int Damage;
        public int Timestamp;
        public bool IsJA;
        public bool IsCritical;

        public Attack(string id, int damage, int timestamp, bool isJa, bool isCritical) {
            this.ID = id;
            this.Damage = damage;
            this.Timestamp = timestamp;
            this.IsJA = isJa;
            this.IsCritical = isCritical;
        }

        public Attack(Attack other) : this(other.ID, other.Damage, other.Timestamp, other.IsJA, other.IsCritical) { }

        public bool hasName {
            get {
                return SkillDictionary.GetInstance().ContainsKey(ID);
            }
        }

        public string Name {
            get {
                return SkillDictionary.GetInstance().Find(ID);
            }
        }

        public string NameOrId {
            get {
                return SkillDictionary.GetInstance().Find(ID, ID);
            }
        }
    }

    public static class AttackExtenstions
    {
        public static IEnumerable<Attack> WithoutZanverse(this IEnumerable<Attack> attacks) {
            if (Properties.Settings.Default.SeparateZanverse) {
                return attacks.Where(a => a.ID != Combatant.ZanverseID);
            } else {
                return attacks;
            }
        }

        public static IEnumerable<Attack> WithoutTurret(this IEnumerable<Attack> attacks) {
            if (Properties.Settings.Default.SeparateTurret) {
                return attacks.Where(a => !Combatant.TurretAttakIDs.Contains(a.ID));
            } else {
                return attacks;
            }
        }

        public static IEnumerable<Attack> WithoutAIS(this IEnumerable<Attack> attacks) {
            if (Properties.Settings.Default.SeparateAIS) {
                return attacks.Where(a => !Combatant.AISAttackIDs.Contains(a.ID));
            } else {
                return attacks;
            }
        }

        public static IEnumerable<Attack> WithoutSeparated(this IEnumerable<Attack> attacks) {
            return attacks.WithoutZanverse().WithoutTurret().WithoutAIS();
        }
    }
}
