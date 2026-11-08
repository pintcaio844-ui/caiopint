using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;

namespace NomesCoerentes
{
    public class Main : BasicMod<Main>
    {
        private static Catalog catalog;
        private static bool warned;
        protected override void OnModLoad()
        {
            catalog = new Catalog(Path.Combine(GetDeclaration().FolderPath, "Nomes"));
            var actor = AccessTools.TypeByName("Actor");
            var meta = AccessTools.TypeByName("MetaType");
            var sex = AccessTools.TypeByName("ActorSex");
            var generator = AccessTools.TypeByName("NameGenerator");
            if (actor == null || meta == null || sex == null || generator == null)
                throw new NotSupportedException("Nomes Coerentes: API de nomes não encontrada nesta versão do WorldBox.");
            var method = AccessTools.Method(generator, "generateName", new[] { actor, meta, typeof(long), sex });
            if (method == null || method.ReturnType != typeof(string))
                throw new NotSupportedException("Nomes Coerentes: assinatura de NameGenerator.generateName mudou. Atualize o mod.");
            new Harmony("bruno.worldbox.nomescoerentes").Patch(method,
                prefix: new HarmonyMethod(typeof(Main), "BeforeName"));
            var harmony = new Harmony("bruno.worldbox.nomescoerentes");
            // Alliances, world names, books, wars and equipment bypass the central route.
            foreach (var candidate in generator.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                var p = candidate.GetParameters();
                if (candidate.ReturnType != typeof(string) || p.Length == 0) continue;
                if (candidate.Name == "getName" && p[0].ParameterType == typeof(string))
                    harmony.Patch(candidate, prefix: new HarmonyMethod(typeof(Main), "BeforeTemplateId"));
                else if (candidate.Name == "generateNameFromTemplate")
                    harmony.Patch(candidate, prefix: new HarmonyMethod(typeof(Main), "BeforeTemplateId"));
            }
            foreach (string typeName in new[] { "Army", "Subspecies" })
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null) continue;
                foreach (var candidate in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (candidate.Name == "generateName" && candidate.ReturnType == typeof(void))
                        harmony.Patch(candidate, prefix: new HarmonyMethod(typeof(Main), "BeforeObjectName"));
            }
            LogInfo("Nomes Coerentes: " + catalog.Count + " nomes completos; integração central instalada. Novos nomes apenas.");
        }

        private static bool BeforeObjectName(object __instance, object[] __args)
        {
            try
            {
                object data = Read(__instance, "data");
                if (Convert.ToBoolean(Read(data, "custom_name") ?? false)) return true;
                bool subspecies = __instance.GetType().Name == "Subspecies";
                string species = subspecies && __args.Length > 0 ? Convert.ToString(Read(__args[0], "id")) : "";
                string name = catalog.Next(subspecies ? "subspecies" : "army", species, "none");
                var setter = AccessTools.Method(__instance.GetType(), "setName", new[] { typeof(string), typeof(bool) });
                if (setter == null || name == null) return true;
                setter.Invoke(__instance, new object[] { name, false });
                return false;
            }
            catch (Exception error)
            {
                if (!warned) { warned = true; UnityEngine.Debug.LogError("Nomes Coerentes: objeto manteve geração original. " + error); }
                return true;
            }
        }

        private static bool BeforeTemplateId(object[] __args, ref string __result)
        {
            string id = __args[0] as string;
            if (id == null) id = Convert.ToString(Read(__args[0], "id"));
            if (string.IsNullOrEmpty(id)) return true;
            id = id.ToLowerInvariant();
            // Mottos and descriptions are text, not object names.
            if (id.Contains("motto")) return true;
            string category = null;
            if (id == "alliance_name") category = "alliance";
            else if (id == "world_name") category = "world";
            else if (id == "player_name") category = "unit";
            else if (id.StartsWith("war_")) category = "war";
            else if (id.StartsWith("book_")) category = "book";
            else if (id.StartsWith("item_") || id.EndsWith("_item")) category = "item";
            if (category == null) return true;
            string name = catalog.Next(category, "", "none");
            if (name == null) return true;
            __result = name;
            return false;
        }

        // Positional arguments avoid depending on game parameter names.
        private static bool BeforeName(object[] __args, ref string __result)
        {
            try
            {
                string category = Convert.ToString(__args[1]).ToLowerInvariant();
                object actor = __args[0];
                object asset = Read(actor, "asset");
                string species = Convert.ToString(Read(asset, "id"));
                string sex = Convert.ToString(__args[3]).ToLowerInvariant();
                if (sex == "none" && actor != null)
                {
                    var female = AccessTools.Method(actor.GetType(), "isSexFemale", Type.EmptyTypes);
                    var male = AccessTools.Method(actor.GetType(), "isSexMale", Type.EmptyTypes);
                    if (female != null && (bool)female.Invoke(actor, null)) sex = "female";
                    else if (male != null && (bool)male.Invoke(actor, null)) sex = "male";
                }
                string name = catalog.Next(category, species, sex);
                if (name == null) return true;
                __result = name;
                return false;
            }
            catch (Exception error)
            {
                if (!warned)
                {
                    warned = true;
                    UnityEngine.Debug.LogError("Nomes Coerentes: falha ao selecionar nome; geração original preservada. " + error);
                }
                return true;
            }
        }

        private static object Read(object target, string member)
        {
            if (target == null) return null;
            var field = AccessTools.Field(target.GetType(), member);
            if (field != null) return field.GetValue(target);
            var property = AccessTools.Property(target.GetType(), member);
            return property == null ? null : property.GetValue(target, null);
        }
    }

    // Only selects complete lines. Never invents syllables or appends numbers.
    internal sealed class Catalog
    {
        private readonly Dictionary<string, Bag> bags = new Dictionary<string, Bag>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> creatures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Random random = new Random();
        public int Count { get; private set; }
        public Catalog(string directory)
        {
            foreach (string file in Directory.GetFiles(directory, "*.txt"))
            {
                string[] values = File.ReadAllLines(file).Select(x => x.Trim())
                    .Where(x => x.Length > 0 && !x.StartsWith("#"))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (values.Length == 0) throw new InvalidDataException("Lista vazia: " + file);
                bags.Add(Path.GetFileNameWithoutExtension(file), new Bag(values, random));
                Count += values.Length;
            }
            foreach (string line in File.ReadAllLines(Path.Combine(directory, "criaturas.tsv")))
            {
                if (line.Trim().Length == 0 || line.StartsWith("#")) continue;
                string[] parts = line.Split('\t');
                if (parts.Length != 2 || !bags.ContainsKey("unit_" + parts[1]))
                    throw new InvalidDataException("Mapeamento de criatura inválido: " + line);
                creatures.Add(parts[0], parts[1]);
            }
            foreach (string category in new[] { "unit", "city", "kingdom", "religion", "culture", "family", "clan", "language", "alliance", "war", "plot", "item", "world", "special", "army", "building", "subspecies" })
                if (!bags.ContainsKey(category + "_other")) throw new InvalidDataException("Lista obrigatória ausente: " + category + "_other");
        }

        public string Next(string category, string species, string sex)
        {
            string race = "other";
            string id = (species ?? "").ToLowerInvariant();
            foreach (string r in new[] { "human", "elf", "orc", "dwarf" })
                if (id == r || id.StartsWith(r + "_", StringComparison.Ordinal)) { race = r; break; }
            if (category == "unit" && race == "other")
            {
                string group;
                if (!creatures.TryGetValue(id, out group))
                {
                    // Derivative assets, such as dragon_*: longest exact prefix wins.
                    string best = null;
                    foreach (var pair in creatures)
                        if (id.StartsWith(pair.Key + "_", StringComparison.Ordinal) && (best == null || pair.Key.Length > best.Length)) best = pair.Key;
                    group = best == null ? "other" : creatures[best];
                }
                race = group;
            }
            Bag bag;
            if ((sex == "male" || sex == "female") && bags.TryGetValue(category + "_" + race + "_" + sex, out bag)) return bag.Next();
            if (bags.TryGetValue(category + "_" + race, out bag)) return bag.Next();
            if (bags.TryGetValue(category + "_other", out bag)) return bag.Next();
            return null; // Unknown future MetaType: no unrelated gameplay changes.
        }

        private sealed class Bag
        {
            private readonly string[] values;
            private readonly Random random;
            private int position;
            private string last;
            public Bag(string[] values, Random random) { this.values = values; this.random = random; position = values.Length; }
            public string Next()
            {
                if (position == values.Length)
                {
                    for (int i = values.Length - 1; i > 0; --i)
                    {
                        int j = random.Next(i + 1);
                        string swap = values[i]; values[i] = values[j]; values[j] = swap;
                    }
                    if (values.Length > 1 && values[0] == last)
                    { string swap = values[0]; values[0] = values[1]; values[1] = swap; }
                    position = 0;
                }
                last = values[position++];
                return last;
            }
        }
    }
}
