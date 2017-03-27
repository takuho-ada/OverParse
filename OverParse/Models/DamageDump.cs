using System;

namespace OverParse.Models
{
    public class DamageDump
    {
        public DamageDump(string csvRow) {
            if (IsHeader = csvRow.StartsWith("timestamp")) {
                return;
            }
            var parts = csvRow.Split(',');
            Timestamp = parts[0];
            InstanceID = parts[1];
            SourceID = parts[2];
            SourceName = parts[3];
            TargetID = parts[4];
            TargetName = parts[5];
            AttackID = parts[6];
            Damage = int.Parse(parts[7]);
            IsJA = parts[8] == "1";
            IsCritical = parts[9] == "1";
            IsMultiHit = parts[10] == "1";
            IsMisc = parts[11] == "1";
            IsMisc2 = parts[12] == "1";
        }

        public string Timestamp { get; private set; }
        public string InstanceID { get; private set; }
        public string SourceID { get; private set; }
        public string SourceName { get; private set; }
        public string TargetID { get; private set; }
        public string TargetName { get; private set; }
        public string AttackID { get; private set; }
        public int Damage { get; private set; }
        public bool IsJA { get; private set; }
        public bool IsCritical { get; private set; }
        public bool IsMultiHit { get; private set; }
        public bool IsMisc { get; private set; }
        public bool IsMisc2 { get; private set; }
        public bool IsHeader { get; private set; }

        public bool IsCurrentPlayerIdData() {
            return Timestamp == "0" && SourceName == "YOU";
        }

        public bool IsInvalidDamageData() {
            return Damage < 1
                || SourceID == "0"
                || AttackID == "0";
        }
    }
}
