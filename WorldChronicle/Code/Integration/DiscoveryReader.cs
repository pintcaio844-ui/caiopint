using System;
using System.Collections.Generic;

namespace WorldChronicle
{
    // Culture traits are native cultural changes, not proof of technological invention.
    // This observer uses Culture's concrete recalc override instead of patching a
    // reference-type generic method shared with religions/languages by Mono.
    internal static class DiscoveryReader
    {
        private sealed class CultureStamp
        {
            internal long Id;
            internal HashSet<string> Traits;
        }

        private static readonly Dictionary<Culture, CultureStamp> Known = new Dictionary<Culture, CultureStamp>();
        private static readonly HashSet<long> KnownBooks = new HashSet<long>();
        private static bool _seeded;

        internal static void Reset()
        {
            Known.Clear();
            KnownBooks.Clear();
            _seeded = false;
        }

        internal static void Seed()
        {
            if (_seeded || GameReader.IsLoading || World.world.cultures == null) return;
            foreach (var culture in World.world.cultures) Remember(culture);
            if (World.world.books != null)
                foreach (var book in World.world.books)
                    if (book != null && book.data != null) KnownBooks.Add(book.data.id);
            _seeded = true;
        }

        internal static bool FirstBookRecord(Book book)
        {
            return book != null && book.data != null && KnownBooks.Add(book.data.id);
        }

        internal static void Remember(Culture culture)
        {
            if (culture == null || culture.data == null) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var trait in culture.getTraits())
                if (trait != null && !string.IsNullOrEmpty(trait.id)) ids.Add(trait.id);
            Known[culture] = new CultureStamp { Id = culture.data.id, Traits = ids };
        }

        internal static List<CultureTrait> NewTraits(Culture culture)
        {
            var added = new List<CultureTrait>();
            if (culture == null || culture.data == null || !culture.isAlive()) return added;
            CultureStamp before;
            if (!Known.TryGetValue(culture, out before) || before.Id != culture.data.id)
            {
                Remember(culture);
                return added;
            }
            foreach (var trait in culture.getTraits())
                if (trait != null && !string.IsNullOrEmpty(trait.id) && !before.Traits.Contains(trait.id)) added.Add(trait);
            Remember(culture);
            return added;
        }

        internal static string NativeName(CultureTrait trait)
        {
            if (trait == null) return null;
            string key = trait.getLocaleID();
            if (!string.IsNullOrEmpty(key) && LocalizedTextManager.stringExists(key))
            {
                string translated = LocalizedTextManager.getText(key, null, false);
                if (!string.IsNullOrWhiteSpace(translated)) return translated;
            }
            // Preserve the game's own identifier when its localization is absent.
            return trait.id;
        }

        internal static BaseSystemData DataFor(NanoObject value)
        {
            var city = value as City;
            if (city != null) return city.data;
            var kingdom = value as Kingdom;
            if (kingdom != null) return kingdom.data;
            var actor = value as Actor;
            if (actor != null) return actor.data;
            var alliance = value as Alliance;
            if (alliance != null) return alliance.data;
            var culture = value as Culture;
            if (culture != null) return culture.data;
            var book = value as Book;
            return book == null ? null : book.data;
        }

        internal static void AttachRuler(ChronicleEvent item, Kingdom kingdom)
        {
            if (item == null || kingdom == null || kingdom.king == null || kingdom.king.data == null || !kingdom.king.isAlive()) return;
            item.ActorId = GameReader.Id(kingdom.king.data);
            item.ActorName = kingdom.king.name;
            item.ActorRole = "ruler_at_event";
        }
    }
}
