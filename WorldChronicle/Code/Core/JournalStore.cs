using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace WorldChronicle
{
    // The save owns the history. Export files are never used to restore a world.
    public static class JournalStore
    {
        public const string SaveKey = "worldchronicle.journal.v1";
        private const string Prefix = "wc1:gzip:";
        private const int MaxBytes = 64 * 1024 * 1024;
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 64,
            Culture = System.Globalization.CultureInfo.InvariantCulture
        };

        public static string Encode(ChronicleJournal journal)
        {
            Validate(journal);
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(journal, Settings));
            if (bytes.Length > MaxBytes) throw new InvalidDataException("A crônica excedeu o limite de 64 MiB; exporte o histórico.");
            using (var result = new MemoryStream())
            {
                using (var zip = new GZipStream(result, CompressionMode.Compress, true))
                    zip.Write(bytes, 0, bytes.Length);
                return Prefix + Convert.ToBase64String(result.ToArray());
            }
        }

        public static ChronicleJournal Decode(string encoded)
        {
            if (String.IsNullOrEmpty(encoded)) throw new InvalidDataException("Crônica vazia.");
            if (encoded.Length > MaxBytes * 2) throw new InvalidDataException("Crônica grande demais para carregar.");
            string json;
            if (encoded.StartsWith(Prefix, StringComparison.Ordinal))
            {
                byte[] packed = Convert.FromBase64String(encoded.Substring(Prefix.Length));
                using (var input = new MemoryStream(packed))
                using (var zip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    byte[] buffer = new byte[8192];
                    int read;
                    while ((read = zip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + read > MaxBytes) throw new InvalidDataException("Crônica descompactada grande demais.");
                        output.Write(buffer, 0, read);
                    }
                    json = new UTF8Encoding(false, true).GetString(output.ToArray());
                }
            }
            else
            {
                if (Encoding.UTF8.GetByteCount(encoded) > MaxBytes)
                    throw new InvalidDataException("Crônica descompactada grande demais.");
                json = encoded;
            }
            var journal = JsonConvert.DeserializeObject<ChronicleJournal>(json, Settings);
            Validate(journal);
            return journal;
        }

        public static void Validate(ChronicleJournal journal)
        {
            if (journal == null || journal.SchemaVersion != 1)
                throw new InvalidDataException("Formato de crônica não reconhecido; o registro original foi preservado.");
            Guid id;
            if (!Guid.TryParse(journal.WorldId, out id) || !Guid.TryParse(journal.BranchId, out id) || journal.Events == null)
                throw new InvalidDataException("Identificação da crônica inválida.");
            long previous = 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in journal.Events)
            {
                if (item == null || item.Sequence <= previous || String.IsNullOrEmpty(item.Id) || !ids.Add(item.Id)
                    || String.IsNullOrEmpty(item.Kind) || (item.Evidence != "direct" && item.Evidence != "observed")
                    || Double.IsNaN(item.WorldTime) || Double.IsInfinity(item.WorldTime))
                    throw new InvalidDataException("Eventos inválidos ou duplicados; a crônica não será substituída.");
                previous = item.Sequence;
            }
            if (journal.LastSequence != previous)
                throw new InvalidDataException("Sequência incompleta da crônica.");
            if (journal.Observations == null || journal.Pages == null)
                throw new InvalidDataException("Dados do livro inválidos.");
            int priorYear = -1;
            foreach (var observation in journal.Observations)
            {
                if (observation == null || observation.Year < 0 || observation.Year <= priorYear || observation.Nations == null)
                    throw new InvalidDataException("Anos de observação inválidos ou duplicados.");
                var nations = new HashSet<string>(StringComparer.Ordinal);
                foreach (var nation in observation.Nations)
                    if (nation == null || String.IsNullOrEmpty(nation.Id) || !nations.Add(nation.Id)
                        || nation.Territory < 0 || nation.Population < 0 || nation.Army < 0)
                        throw new InvalidDataException("Dados anuais de nações inválidos.");
                priorYear = observation.Year;
            }
            int priorCentury = 0;
            foreach (var page in journal.Pages)
            {
                if (page == null || page.CenturyIndex <= priorCentury || page.StartYear < 0
                    || page.CenturyIndex != page.StartYear / 100 + 1 || page.StartYear % 100 != 0
                    || (long)page.EndYear != (long)page.StartYear + 99 || page.ObservedYears < 0 || page.ObservedYears > 100
                    || page.ObservedThroughYear < page.StartYear || page.ObservedThroughYear > page.EndYear
                    || Double.IsNaN(page.DominanceScore) || Double.IsInfinity(page.DominanceScore)
                    || page.DominanceScore < 0 || page.DominanceScore > 1.000000001 || page.Rankings == null)
                    throw new InvalidDataException("Página histórica inválida.");
                var rankedIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var rank in page.Rankings)
                    if (rank == null || String.IsNullOrEmpty(rank.Id) || !rankedIds.Add(rank.Id)
                        || Double.IsNaN(rank.Score) || Double.IsInfinity(rank.Score) || rank.Score < 0 || rank.Score > 1.000000001
                        || rank.YearsPresent < 0 || rank.YearsPresent > page.ObservedYears)
                        throw new InvalidDataException("Comparação histórica de nações inválida.");
                priorCentury = page.CenturyIndex;
            }
        }
    }
}
