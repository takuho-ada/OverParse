using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace OverParse.Models
{
    public sealed class SkillDictionary
    {
        public enum LanguageEnum
        {
            EN,
            JA,
        }

        private static readonly SkillDictionary instance = new SkillDictionary();

        private readonly IDictionary<string, string> dic = new Dictionary<string, string>();

        public static SkillDictionary GetInstance() {
            return instance;
        }

        private SkillDictionary() { }

        private void parse(FileInfo skillCsv) {
            Console.WriteLine($"Parsing {skillCsv.Name}");
            dic.Clear();
            foreach (var line in File.ReadLines(skillCsv.FullName)) {
                string[] fields = line.Split(',');
                if (fields.Length > 1) {
                    dic.Add(/* ID */ fields[1], /* Type */ fields[0]);
                }
            }
            Console.WriteLine("Keys in skill dict: " + dic.Count());
        }

        public void Initialize(LanguageEnum lang, Action<bool, FileInfo> callback = null) {
            var skillCsv = SkillCSV(lang);
            var skillUri = SkillCSVUrl(lang);

            if (skillCsv.Exists) {
                parse(skillCsv);
            }

            Console.WriteLine($"Updating {skillCsv.Name}");
            var client = new WebClient();
            client.OpenReadCompleted += (sender, e) => {
                if (e.Error != null || e.Cancelled) {
                    Console.WriteLine($"{skillCsv.Name} update failed: {e}");
                    callback?.Invoke(false, skillCsv);
                    return;
                }
                using (var reader = new StreamReader(e.Result)) {
                    try {
                        File.WriteAllText(skillCsv.FullName, reader.ReadToEnd());
                    } catch (Exception ex) {
                        Console.WriteLine($"{skillCsv.Name} update failed: {ex}");
                        callback?.Invoke(false, skillCsv);
                        return;
                    }
                }
                parse(skillCsv);
                callback?.Invoke(true, skillCsv);
            };
            client.OpenReadAsync(skillUri);
        }

        public string Find(string id, string defValue = "Unknown") {
            if (dic.ContainsKey(id)) {
                return dic[id];
            } else {
                return defValue;
            }
        }

        public bool ContainsKey(string id) {
            return dic.ContainsKey(id);
        }

        private FileInfo SkillCSV(LanguageEnum lang) {
            switch (lang) {
            case LanguageEnum.JA:
                return new FileInfo("skills.ja.csv");
            default:
                return new FileInfo("skills.csv");
            }
        }

        private Uri SkillCSVUrl(LanguageEnum lang) {
            switch (lang) {
            case LanguageEnum.JA:
                return new Uri("https://raw.githubusercontent.com/nemomomo/PSO2ACT/master/PSO2ACT/skills.csv");
            default:
                return new Uri("https://raw.githubusercontent.com/VariantXYZ/PSO2ACT/master/PSO2ACT/skills.csv");
            }
        }
    }
}
