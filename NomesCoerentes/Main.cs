using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NeoModLoader.api;

namespace NomesCoerentes
{
    public class Main : BasicMod<Main>
    {
        private static Catalog catalog;
        private static bool warned;
        [ThreadStatic] private static bool correcting;
        private static readonly ConditionalWeakTable<object, NameStamp> checkedNames = new ConditionalWeakTable<object, NameStamp>();
        private sealed class NameStamp { public string Name; public string Species; public string FamilyName; }
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
            // Explicit lifecycle hooks cover culture/onomastics and preassigned names.
            PatchPostfix(harmony, actor, "generateNewName", "AfterActorBirth");
            PatchPostfix(harmony, actor, "getName", "AfterActorRead");
            PatchPostfix(harmony, actor, "setFamily", "AfterActorBirth");
            PatchPostfix(harmony, actor, "generateName", "AfterActorGenerated");
            foreach (var pair in new[] { "City:generateName", "Kingdom:newCivKingdom", "Culture:createCulture", "Clan:newClan", "Family:generateName", "Religion:generateName", "Language:generateName" })
            {
                var parts = pair.Split(':');
                PatchPostfix(harmony, AccessTools.TypeByName(parts[0]), parts[1], "AfterMetaCreated");
            }
            LogInfo("Nomes Coerentes: " + catalog.Count + " nomes completos; integração central e caminhos diretos instalados (personagens e quatro raças).");
        }

        private static void PatchPostfix(Harmony harmony, Type type, string name, string handler)
        {
            if (type == null) return;
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (method.Name == name && (handler != "AfterActorRead" || method.ReturnType == typeof(string)))
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(Main), handler));
        }

        private static string Species(object actor)
        {
            object asset = Read(actor, "asset");
            string template = Convert.ToString(Read(asset, "name_template_unit"));
            string race = Catalog.Race(template);
            return race != "other" ? race : Convert.ToString(Read(asset, "id"));
        }

        private static string Sex(object actor)
        {
            return Convert.ToString(Read(Read(actor, "data"), "sex")).ToLowerInvariant();
        }

        private static string CorrectObject(object target, string category, object actor)
        {
            if (correcting || target == null) return null;
            object data = Read(target, "data");
            if (data == null || Convert.ToBoolean(Read(data, "custom_name") ?? false)) return null;
            string species = Species(actor);
            string current = Convert.ToString(Read(data, "name"));
            NameStamp stamp = checkedNames.GetValue(data, key => new NameStamp());
            bool person = category == "unit" && Catalog.Race(species) != "other";
            string familyName = person ? Convert.ToString(Read(Read(Read(actor, "family"), "data"), "name")).Trim() : "";
            if (stamp.Name == current && stamp.Species == species && stamp.FamilyName == familyName) return current;
            if (person) return PersonalName(target, data, species, current, familyName, stamp);
            if (catalog.Contains(category, species, current))
            { stamp.Name = current; stamp.Species = species; stamp.FamilyName = familyName; return current; }
            var setter = AccessTools.Method(target.GetType(), "setName", new[] { typeof(string), typeof(bool) });
            if (setter == null) return null;
            correcting = true;
            try
            {
                string value = catalog.Next(category, species, Sex(actor));
                if (value == null) return null;
                // The second argument is pTrack, NOT a custom-name flag.
                setter.Invoke(target, new object[] { value, true });
                stamp.Name = value; stamp.Species = species; stamp.FamilyName = familyName;
                return value;
            }
            finally { correcting = false; }
        }

        // The surname is exactly the current vanilla Family name. No inheritance rules change.
        private static string PersonalName(object actor, object data, string species, string current, string familyName, NameStamp stamp)
        {
            string given = catalog.Contains("unit", species, current) ? current : SavedGiven(data);
            if (string.IsNullOrEmpty(given) && familyName.Length > 0 && current.EndsWith(" " + familyName, StringComparison.Ordinal))
            {
                string candidate = current.Substring(0, current.Length - familyName.Length - 1);
                if (catalog.Contains("unit", species, candidate)) given = candidate;
            }
            if (string.IsNullOrEmpty(given)) given = catalog.Next("unit", species, Sex(actor));
            if (string.IsNullOrEmpty(given)) return current;
            // No family yet: leave the existing full name intact while save data is loading.
            string value = familyName.Length == 0 && !string.IsNullOrEmpty(SavedGiven(data)) && current.StartsWith(given + " ", StringComparison.Ordinal)
                ? current : given + (familyName.Length == 0 ? "" : " " + familyName);
            var setter = AccessTools.Method(actor.GetType(), "setName", new[] { typeof(string), typeof(bool) });
            var persist = AccessTools.Method(data.GetType(), "set", new[] { typeof(string), typeof(string) });
            if (setter == null || persist == null) throw new NotSupportedException("Nomes Coerentes: API de persistência do prenome não encontrada.");
            correcting = true;
            try
            {
                persist.Invoke(data, new object[] { "nomescoerentes.given_name", given });
                if (value != current) setter.Invoke(actor, new object[] { value, true });
                stamp.Name = value; stamp.Species = species; stamp.FamilyName = familyName;
                return value;
            }
            finally { correcting = false; }
        }

        private static string SavedGiven(object data)
        {
            var get = AccessTools.Method(data.GetType(), "get", new[] { typeof(string), typeof(string).MakeByRefType(), typeof(string) });
            if (get == null) return "";
            object[] args = { "nomescoerentes.given_name", "", "" };
            get.Invoke(data, args);
            return Convert.ToString(args[1]);
        }

        private static void AfterActorBirth(object __instance) { CorrectSafely(__instance, "unit", __instance); }
        private static void AfterActorRead(object __instance, ref string __result)
        {
            string name = CorrectSafely(__instance, "unit", __instance);
            if (name != null) __result = name;
        }
        private static void AfterActorGenerated(object __instance, object[] __args, ref string __result)
        {
            string category = Convert.ToString(__args[0]).ToLowerInvariant();
            string species = Species(__instance);
            if (!catalog.Contains(category, species, __result))
            {
                string value = catalog.Next(category, species, __args.Length > 2 ? Convert.ToString(__args[2]).ToLowerInvariant() : Sex(__instance));
                if (value != null) __result = value;
            }
        }
        private static void AfterMetaCreated(object __instance, object[] __args)
        {
            CorrectSafely(__instance, __instance.GetType().Name.ToLowerInvariant(), __args.Length > 0 ? __args[0] : null);
        }
        private static string CorrectSafely(object target, string category, object actor)
        {
            try { return CorrectObject(target, category, actor); }
            catch (Exception error)
            {
                if (!warned) { warned = true; UnityEngine.Debug.LogError("Nomes Coerentes: falha no caminho de personagens/metas. " + error); }
                return null;
            }
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
            string species = Catalog.Race(id);
            if (category == null)
                foreach (string c in new[] { "unit", "city", "kingdom", "religion", "culture", "family", "clan", "language" })
                    if (id.EndsWith("_" + c, StringComparison.Ordinal)) { category = c; break; }
            if (category == null) return true;
            object actor = __args.FirstOrDefault(x => x != null && x.GetType().Name == "Actor");
            if (species == "other" && actor != null) species = Species(actor);
            string name = catalog.Next(category, species, actor == null ? "none" : Sex(actor));
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
                string species = Species(actor);
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

        public static string Race(string value)
        {
            string[] tokens = (value ?? "").ToLowerInvariant().Split(new[] { '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                if (token == "human" || token == "humans") return "human";
                if (token == "elf" || token == "elves") return "elf";
                if (token == "dwarf" || token == "dwarves" || token == "dwarfs") return "dwarf";
                if (token == "orc" || token == "orcs") return "orc";
            }
            return "other";
        }
        public bool Contains(string category, string species, string value)
        {
            string race = Race(species);
            if (category == "unit" && race == "other")
            {
                string id = (species ?? "").ToLowerInvariant();
                string group;
                if (creatures.TryGetValue(id, out group)) race = group;
                else
                {
                    var best = creatures.Keys.Where(k => id.StartsWith(k + "_", StringComparison.Ordinal)).OrderByDescending(k => k.Length).FirstOrDefault();
                    if (best != null) race = creatures[best];
                }
            }
            Bag bag;
            if (bags.TryGetValue(category + "_" + race, out bag)) return bag.Contains(value);
            return bags.TryGetValue(category + "_other", out bag) && bag.Contains(value);
        }

        public string Next(string category, string species, string sex)
        {
            string id = (species ?? "").ToLowerInvariant();
            string race = Race(id);
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
            private readonly HashSet<string> names;
            public bool Contains(string value) { return names.Contains(value ?? ""); }
            private int position;
            private string last;
            public Bag(string[] values, Random random) { this.values = values; this.random = random; names = new HashSet<string>(values, StringComparer.Ordinal); position = values.Length; }
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
