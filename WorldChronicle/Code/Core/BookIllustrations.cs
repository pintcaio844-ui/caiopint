using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldChronicle
{
    public static class BookIllustrations
    {
        public static bool IsPng(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length > 65536) return false;
            try
            {
                var bytes = Convert.FromBase64String(value);
                return bytes.Length > 24 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71
                    && bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10;
            }
            catch (FormatException) { return false; }
        }

        public static List<RealmIllustration> ForPage(ChronicleJournal journal, CenturyPage page)
        {
            var source = journal.Illustrations ?? new List<RealmIllustration>();
            return source.Where(x => x != null && x.CenturyIndex == page.CenturyIndex && IsPng(x.PngBase64))
                .OrderByDescending(x => page.Rankings.Where(r => r.Id == x.NationId).Select(r => r.Score).FirstOrDefault())
                .ThenBy(x => x.NationId, StringComparer.Ordinal).Take(3).ToList();
        }
    }
}
