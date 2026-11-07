using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace WorldChronicle
{
    internal static class ChronicleRuntime
    {
        private static MapStats _stats;
        private static ChronicleJournal _journal;
        private static bool _suspended = true;
        private static long _revision;
        private static long _savedRevision = -1;
        private static bool _bookDirty;
        private static int _lastAttemptedYear = -1;
        public static string Status = "Carregue um mundo para começar a crônica.";
        public static ChronicleJournal Journal { get { return _journal; } }
        public static long Revision { get { return _revision; } }
        public static List<CenturyPage> BookPages
        {
            get { RefreshBook(); return _journal == null ? null : _journal.Pages; }
        }
        public static string ExportRoot { get { return Path.Combine(Application.persistentDataPath, "WorldChronicle", "Exports"); } }
        public static bool CanRecord
        {
            get { return !_suspended && _journal != null && System.Object.ReferenceEquals(_stats, GameReader.Stats) && !GameReader.IsLoading; }
        }

        public static void OnWorldChanging()
        {
            _suspended = true;
            _stats = null;
            _journal = null;
            _lastAttemptedYear = -1;
            Status = "Aguardando o carregamento do livro deste mundo.";
        }

        public static void OnWorldReady()
        {
            try
            {
                var stats = GameReader.Stats;
                if (stats == null) return;
                if (System.Object.ReferenceEquals(_stats, stats) && _journal != null && !_suspended) return;
                _suspended = true;
                _stats = stats;
                _journal = null;
                _savedRevision = -1;
                _revision = 0;
                _lastAttemptedYear = -1;
                string packed = null;
                if (stats.custom_data != null) stats.custom_data.get(JournalStore.SaveKey, out packed, null);
                bool exists = packed != null;
                if (exists)
                {
                    // Read only this checkpoint, never an export or a newer save's history.
                    _journal = JournalStore.Decode(packed);
                    _journal.BranchId = Guid.NewGuid().ToString("N");
                }
                else
                {
                    _journal = new ChronicleJournal
                    {
                        WorldId = Guid.NewGuid().ToString("N"), BranchId = Guid.NewGuid().ToString("N"),
                        WorldName = GameReader.WorldName, StartedYear = GameReader.Year
                    };
                }
                _journal.WorldName = GameReader.WorldName;
                _bookDirty = true;
                _suspended = false;
                if (!exists)
                    Record(new ChronicleEvent { Kind = "observation_started", Evidence = "observed",
                        SubjectName = GameReader.WorldName, Detail = GameReader.Summary() });
                TickBook();
                Status = "O livro deste mundo está sendo escrito.";
                OnBeforeSave();
            }
            catch (Exception ex)
            {
                _suspended = true;
                _journal = null;
                Status = "Registro pausado: " + ex.Message;
                Debug.LogError("[WorldChronicle] Falha ao restaurar; dados do save preservados. " + ex);
            }
        }

        public static void Record(ChronicleEvent item)
        {
            try
            {
                if (item == null || !CanRecord) return;
                item.Year = GameReader.Year;
                item.WorldTime = GameReader.Time;
                item.Id = Guid.NewGuid().ToString("N");
                item.Sequence = _journal.LastSequence + 1;
                if (String.IsNullOrEmpty(item.Evidence)) item.Evidence = "direct";
                _journal.Events.Add(item);
                _journal.LastSequence = item.Sequence;
                _bookDirty = true;
                _revision++;
            }
            catch (Exception ex) { Debug.LogError("[WorldChronicle] Evento não registrado: " + ex); }
        }

        // Annual snapshots measure lasting influence, including nations that later vanish.
        // Never synthesize observations for years skipped by fast simulation or time tools.
        public static void TickBook()
        {
            if (!CanRecord) return;
            int year = GameReader.Year;
            if (year == _lastAttemptedYear) return;
            if (year < _journal.CurrentYear && _journal.Observations.Count > 0)
            {
                _suspended = true;
                Status = "O ano retrocedeu sem carregar um save. Carregue a partida desejada para continuar seu livro.";
                return;
            }
            _lastAttemptedYear = year;
            _journal.CurrentYear = year;
            _bookDirty = true;
            _revision++;
            try
            {
                var observations = _journal.Observations;
                if (observations.Count == 0 || observations[observations.Count - 1].Year < year)
                {
                    AnnualObservation observation = GameReader.ObserveNations();
                    observations.Add(observation);
                    RealmArt.Observe(_journal, observation);
                    if (_journal.MetricsStartedYear < 0) _journal.MetricsStartedYear = year;
                }
            }
            catch (Exception ex)
            {
                Status = "Não foi possível medir as nações neste ano; o livro indicará os dados ausentes.";
                Debug.LogWarning("[WorldChronicle] Observação anual indisponível: " + ex);
            }
            RefreshBook();
        }

        private static void RefreshBook()
        {
            if (_journal == null || !_bookDirty) return;
            try
            {
                int completedBefore = _journal.Pages.FindAll(page => page.IsComplete).Count;
                CenturyBook.Refresh(_journal, _journal.CurrentYear);
                _bookDirty = false;
                int completedAfter = _journal.Pages.FindAll(page => page.IsComplete).Count;
                if (completedAfter > completedBefore)
                {
                    Status = "Um século terminou. Seu resumo foi acrescentado ao livro do mundo.";
                    Debug.Log("[WorldChronicle] Livro atualizado: " + completedAfter + " séculos concluídos.");
                }
            }
            catch (Exception ex)
            {
                Status = "Não foi possível atualizar uma página do livro: " + ex.Message;
                Debug.LogError("[WorldChronicle] Falha ao resumir século: " + ex);
            }
        }

        public static void OnBeforeSave()
        {
            try
            {
                if (!CanRecord) return;
                TickBook();
                if (!CanRecord) return;
                RefreshBook();
                _journal.WorldName = GameReader.WorldName;
                // Refresh on every save: BranchId and world name may have changed.
                string packed = JournalStore.Encode(_journal);
                if (_stats.custom_data == null) _stats.custom_data = new SaveCustomData();
                _stats.custom_data.set(JournalStore.SaveKey, packed);
                _savedRevision = _revision;
                Status = "Livro preparado para salvar junto ao mundo.";
            }
            catch (Exception ex)
            {
                Status = "Não foi possível atualizar a crônica no save: " + ex.Message;
                Debug.LogError("[WorldChronicle] Falha ao preparar crônica para save; jogo continua. " + ex);
            }
        }

        public static string SaveStatus
        {
            get { return _journal == null ? Status : (_revision == _savedRevision
                ? "O livro acompanha o próximo salvamento do mundo."
                : "Há páginas ou fatos novos. Salve o mundo para preservá-los na partida."); }
        }

        public static string Export()
        {
            if (_journal == null) throw new InvalidOperationException("Nenhum mundo está sendo registrado.");
            _journal.WorldName = GameReader.WorldName;
            TickBook();
            RefreshBook();
            // Both identifiers are validated GUIDs, never player-controlled path components.
            JournalStore.Validate(_journal);
            string folder = Path.Combine(ExportRoot, _journal.WorldId, _journal.BranchId);
            Directory.CreateDirectory(folder);
            WriteAtomic(Path.Combine(folder, "livro.html"), CenturyBook.Html(_journal));
            WriteAtomic(Path.Combine(folder, "livro.md"), CenturyBook.Markdown(_journal));
            WriteAtomic(Path.Combine(folder, "cronica.html"), Narrative.Html(_journal));
            WriteAtomic(Path.Combine(folder, "cronica.md"), Narrative.Markdown(_journal));
            WriteAtomic(Path.Combine(folder, "eventos.json"), JsonConvert.SerializeObject(_journal, Formatting.Indented));
            Status = "Livro exportado: " + folder;
            return folder;
        }

        private static void WriteAtomic(string path, string content)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
