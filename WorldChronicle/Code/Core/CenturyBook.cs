using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WorldChronicle
{
    /// <summary>
    /// Turns the journal into century-sized pages. A completed page is a historical
    /// checkpoint: subsequent refreshes retain that exact page rather than rewriting it.
    /// </summary>
    public static class CenturyBook
    {
        private const double TieTolerance = 0.000000001;
        private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");

        public static void Refresh(ChronicleJournal journal, int currentYear)
        {
            if (journal == null) throw new ArgumentNullException("journal");
            journal.CurrentYear = currentYear;
            if (journal.Pages == null) journal.Pages = new List<CenturyPage>();
            if (journal.Observations == null) journal.Observations = new List<AnnualObservation>();

            var completed = new Dictionary<int, CenturyPage>();
            foreach (var page in journal.Pages)
            {
                if (page != null && page.IsComplete && !completed.ContainsKey(page.CenturyIndex))
                    completed.Add(page.CenturyIndex, page);
            }
            var pages = completed.Values.ToList();
            if (currentYear >= journal.StartedYear)
            {
                int first = IndexOf(journal.StartedYear);
                int last = IndexOf(currentYear);
                for (int index = first; index <= last; index++)
                {
                    if (!completed.ContainsKey(index)) pages.Add(BuildPage(journal, index, currentYear));
                    // Defensive guard for an integer-boundary input; real game centuries
                    // are far below this value.
                    if (index == int.MaxValue) break;
                }
            }
            journal.Pages = pages.OrderBy(x => x.CenturyIndex).ToList();
        }

        private static CenturyPage BuildPage(ChronicleJournal journal, int index, int currentYear)
        {
            long start = ((long)index - 1) * 100;
            int firstYear = ClampYear(start);
            int lastYear = ClampYear(start + 99);
            var page = new CenturyPage
            {
                CenturyIndex = index,
                StartYear = firstYear,
                EndYear = lastYear,
                ObservedThroughYear = Math.Min(currentYear, lastYear),
                IsComplete = (long)currentYear > start + 99
            };

            // An annual snapshot contributes once. If a malformed/imported journal
            // repeats a year, the last snapshot wins instead of increasing its weight.
            var years = new Dictionary<int, AnnualObservation>();
            int observedFrom = Math.Max(firstYear, journal.StartedYear);
            if (journal.MetricsStartedYear >= 0) observedFrom = Math.Max(observedFrom, journal.MetricsStartedYear);
            foreach (var observation in journal.Observations)
            {
                if (observation == null || observation.Year < observedFrom || observation.Year > page.ObservedThroughYear) continue;
                years[observation.Year] = observation;
            }
            page.ObservedYears = years.Count;
            page.PartialCoverage = years.Count < ((long)lastYear - firstYear + 1);
            page.Rankings = Rank(years.Values.OrderBy(x => x.Year).ToList());
            SelectDominance(page);

            var events = (journal.Events ?? new List<ChronicleEvent>())
                .Where(x => x != null && x.Year >= Math.Max(firstYear, journal.StartedYear)
                    && x.Year <= page.ObservedThroughYear)
                .OrderBy(x => x.Sequence).ToList();
            page.Title = Title(page, events);
            page.RecordText = Narrative.FactsText(events);
            page.Summary = Summary(journal, page, events);
            return page;
        }

        private static List<NationStanding> Rank(List<AnnualObservation> observations)
        {
            var standings = new Dictionary<string, NationStanding>(StringComparer.Ordinal);
            foreach (var observation in observations)
            {
                var nations = new Dictionary<string, NationSnapshot>(StringComparer.Ordinal);
                if (observation.Nations != null)
                {
                    foreach (var nation in observation.Nations)
                    {
                        if (nation != null && Has(nation.Id)) nations[nation.Id] = nation;
                    }
                }

                double totalTerritory = nations.Values.Sum(x => (double)Math.Max(0, x.Territory));
                double totalPopulation = nations.Values.Sum(x => (double)Math.Max(0, x.Population));
                double totalArmy = nations.Values.Sum(x => (double)Math.Max(0, x.Army));
                foreach (var nation in nations.Values)
                {
                    NationStanding standing;
                    if (!standings.TryGetValue(nation.Id, out standing))
                    {
                        standing = new NationStanding { Id = nation.Id, Name = NationName(nation), Score = 0 };
                        standings.Add(nation.Id, standing);
                    }
                    // Identity stays tied to the ID even when a mod or the player renames
                    // the nation. The most recent observed name labels the ranking.
                    standing.Name = NationName(nation);
                    standing.YearsPresent++;
                    standing.Score += Share(nation.Territory, totalTerritory) * 0.5
                        + Share(nation.Population, totalPopulation) * 0.3
                        + Share(nation.Army, totalArmy) * 0.2;
                }
            }

            foreach (var standing in standings.Values)
            {
                // The denominator includes years when this nation was absent. Dividing
                // by YearsPresent would incorrectly favor a short-lived conqueror.
                standing.Score = observations.Count == 0 ? 0 : standing.Score / observations.Count;
                standing.Score = Math.Max(0, Math.Min(1, standing.Score));
            }
            return standings.Values.OrderByDescending(x => x.Score)
                .ThenBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
        }

        private static void SelectDominance(CenturyPage page)
        {
            if (page.Rankings.Count == 0 || page.Rankings[0].Score <= TieTolerance) return;
            double best = page.Rankings[0].Score;
            var leaders = page.Rankings.Where(x => Math.Abs(x.Score - best) <= TieTolerance).ToList();
            page.DominanceScore = best;
            page.DominantNationName = JoinNames(leaders.Select(x => x.Name).ToList());
            page.DominantNationId = leaders.Count == 1 ? leaders[0].Id : null;
        }

        private static string Title(CenturyPage page, List<ChronicleEvent> events)
        {
            var ended = OfKind(events, "kingdom_ended").Where(x => Has(x.SubjectName))
                .OrderByDescending(x => page.Rankings.Where(r => r.Id == x.SubjectId).Select(r => r.Score).FirstOrDefault())
                .ThenBy(x => x.Sequence).FirstOrDefault();
            if (ended != null) return "O fim de " + ended.SubjectName;
            if (Count(events, "rebellion") > 0) return "Rupturas e novos poderes";
            if (Count(events, "war_started") > 0 && Count(events, "city_captured") > 0)
                return "Guerras e mudanças de fronteira";
            if (Count(events, "city_captured") > 0) return "A disputa pelo território";
            if (Count(events, "war_started") > 0 || Count(events, "war_ended") > 0) return "As marcas da guerra";
            if (Count(events, "kingdom_founded") > 0) return "O surgimento de novos reinos";
            if (Count(events, "alliance_formed") > 0 || Count(events, "alliance_ended") > 0)
                return "As relações entre os reinos";
            if (events.Any(x => x.Kind == "ruler_changed" && Direct(x) && Has(x.OtherId)))
                return "A sucessão dos governantes";
            if (Has(page.DominantNationId)) return "A projeção de " + page.DominantNationName;
            if (Has(page.DominantNationName)) return "Lideranças compartilhadas";
            if (page.ObservedYears > 0) return "Um retrato das nações";
            if (events.Count > 0) return "Os primeiros registros";
            return page.IsComplete ? "Um século sem registros" : "Uma história em curso";
        }

        private static string Summary(ChronicleJournal journal, CenturyPage page, List<ChronicleEvent> events)
        {
            var paragraphs = new List<string>();
            var opening = new StringBuilder();
            if (page.IsComplete)
                opening.Append("Este capítulo acompanha os anos ").Append(N(page.StartYear)).Append(" a ")
                    .Append(N(page.EndYear)).Append(". ");
            else
                opening.Append("O século dos anos ").Append(N(page.StartYear))
                    .Append(" a ").Append(N(page.EndYear)).Append(" ainda está em andamento; a crônica chega ao ano ")
                    .Append(N(page.ObservedThroughYear)).Append(". ");

            if (journal.StartedYear > page.StartYear)
                opening.Append("A crônica começou no ano ").Append(N(journal.StartedYear))
                    .Append("; a história anterior não foi acompanhada. ");
            paragraphs.Add(opening.ToString());

            if (Has(page.DominantNationName))
            {
                if (Has(page.DominantNationId))
                    paragraphs.Add("Nos anos acompanhados, " + page.DominantNationName
                        + " foi a potência de maior projeção deste mundo.");
                else
                    paragraphs.Add(page.DominantNationName + " dividiram a liderança dos anos acompanhados.");
            }
            else if (events.Any(x => Historical(x.Kind)) || page.ObservedYears > 0)
                paragraphs.Add("Não foi possível determinar uma nação dominante neste trecho da história.");

            var arc = ExtinctionArc(journal, page, events);
            string linkedHistory = HistoricalLinks.Describe(journal, page);
            if (Has(linkedHistory)) paragraphs.Add(linkedHistory);
            if (Has(arc)) paragraphs.Add(arc);
            AddWithinLimit(paragraphs, ConflictSummary(journal, events, page.Rankings), 300);
            AddWithinLimit(paragraphs, PoliticalSummary(events), 300);
            AddWithinLimit(paragraphs, DiscoverySummary(events), 300);
            AddWithinLimit(paragraphs, RelationsSummary(events), 300);

            bool hasHistoricalFacts = events.Any(x => Historical(x.Kind) && (Direct(x) || x.Kind == "discovery"));
            if (!hasHistoricalFacts)
            {
                if (page.ObservedYears > 0)
                    paragraphs.Add("Os registros descrevem as nações deste período, mas não preservam acontecimentos suficientes para narrar suas transformações.");
                else
                    paragraphs.Add("Este período não foi acompanhado; não há registros suficientes para contar sua história.");
            }
            return string.Join("\n\n", paragraphs.ToArray());
        }

        private static string ExtinctionArc(ChronicleJournal journal, CenturyPage page, List<ChronicleEvent> events)
        {
            var ended = OfKind(events, "kingdom_ended").Where(x => Has(x.SubjectId) && Has(x.SubjectName) && KingdomType(x.SubjectType))
                .OrderByDescending(x => page.Rankings.Where(r => r.Id == x.SubjectId).Select(r => r.Score).FirstOrDefault())
                .ThenBy(x => x.Sequence).FirstOrDefault();
            if (ended == null) return string.Empty;
            var history = (journal.Events ?? new List<ChronicleEvent>())
                .Where(x => x != null && x.Sequence <= ended.Sequence && x.Year <= ended.Year && Direct(x))
                .OrderBy(x => x.Sequence).ToList();
            var text = new StringBuilder();
            text.Append("Uma das grandes transformações do período foi o fim de ").Append(ended.SubjectName)
                .Append(", registrado no ano ").Append(N(ended.Year)).Append(". ");
            var founded = history.FirstOrDefault(x => x.Kind == "kingdom_founded" && x.SubjectId == ended.SubjectId && KingdomType(x.SubjectType));
            if (founded != null)
            {
                text.Append("Sua fundação havia sido registrada no ano ").Append(N(founded.Year));
                if (Has(founded.SubjectName) && founded.SubjectName != ended.SubjectName)
                    text.Append(", com o nome ").Append(founded.SubjectName);
                text.Append(". ");
            }
            var renamed = history.LastOrDefault(x => x.Kind == "name_changed" && x.SubjectType == "KingdomData"
                && x.SubjectId == ended.SubjectId && Has(x.OtherName) && Has(x.SubjectName));
            if (renamed != null)
                text.Append("No ano ").Append(N(renamed.Year)).Append(", ").Append(renamed.OtherName)
                    .Append(" passou a chamar-se ").Append(renamed.SubjectName).Append(". ");
            var loss = history.LastOrDefault(x => x.Kind == "city_captured" && x.ContextId == ended.SubjectId && KingdomType(x.ContextType)
                && Has(x.SubjectName) && Has(x.OtherName));
            if (loss != null)
            {
                text.Append("Antes de desaparecer, o reino havia perdido a cidade de ").Append(loss.SubjectName)
                    .Append(" para ").Append(loss.OtherName).Append(" no ano ").Append(N(loss.Year)).Append(". ");
            }
            var war = history.LastOrDefault(x => x.Kind == "war_started" && ((x.OtherId == ended.SubjectId && KingdomType(x.OtherType))
                    || (x.ContextId == ended.SubjectId && KingdomType(x.ContextType)))
                && Has(x.SubjectName));
            if (war != null)
                text.Append("Os registros também o situam na guerra ").Append(war.SubjectName)
                    .Append(", iniciada no ano ").Append(N(war.Year)).Append(". ");
            var ruler = history.LastOrDefault(x => x.Kind == "ruler_changed" && x.SubjectId == ended.SubjectId && KingdomType(x.SubjectType)
                && Has(x.OtherName) && Has(x.OtherId));
            if (ruler != null)
                text.Append("Entre seus governantes registrados esteve ").Append(ruler.OtherName)
                    .Append(", que assumiu no ano ").Append(N(ruler.Year)).Append(". ");
            return text.ToString().Trim();
        }

        private static string PoliticalSummary(List<ChronicleEvent> events)
        {
            var founded = OfKind(events, "kingdom_founded");
            var ended = OfKind(events, "kingdom_ended");
            var revolts = OfKind(events, "rebellion");
            var rulers = events.Where(x => Direct(x) && x.Kind == "ruler_changed" && Has(x.OtherId)).ToList();
            var cities = OfKind(events, "city_founded");
            var parts = new List<string>();
            if (founded.Count > 0)
                parts.Add(string.Join(" ", founded.Where(x => Has(x.SubjectName)).Take(2).Select(Dated).ToArray()));
            if (ended.Count > 0)
                parts.Add(string.Join(" ", ended.Where(x => Has(x.SubjectName)).Take(2).Select(Dated).ToArray()));
            if (revolts.Count > 0)
                parts.Add(string.Join(" ", revolts.Where(x => Has(x.SubjectName)).Take(2).Select(Dated).ToArray()));
            if (rulers.Count > 0)
            {
                var ruler = rulers.FirstOrDefault(x => Has(x.OtherName) && Has(x.SubjectName));
                if (ruler != null)
                    parts.Add(ruler.OtherName + " passou a governar " + ruler.SubjectName + " no ano " + N(ruler.Year) + ".");
            }
            if (cities.Count > 0)
                parts.Add(string.Join(" ", cities.Where(x => Has(x.SubjectName)).Take(2).Select(Dated).ToArray()));
            return string.Join(" ", parts.ToArray());
        }

        private static string ConflictSummary(ChronicleJournal journal, List<ChronicleEvent> events, List<NationStanding> rankings)
        {
            var started = OfKind(events, "war_started");
            var ended = OfKind(events, "war_ended");
            var captured = OfKind(events, "city_captured");
            var destroyed = OfKind(events, "city_destroyed");
            var parts = new List<string>();
            if (started.Count > 0 || ended.Count > 0)
            {
                var rankById = rankings.ToDictionary(x => x.Id, x => x.Score, StringComparer.Ordinal);
                var notable = started.Concat(ended).Where(x => Has(x.SubjectName))
                    .OrderByDescending(x => Standing(rankById, x.OtherId) + Standing(rankById, x.ContextId))
                    .ThenBy(x => x.Sequence).FirstOrDefault();
                if (notable != null)
                {
                    var origin = (journal.Events ?? new List<ChronicleEvent>()).FirstOrDefault(x => x != null && Direct(x)
                        && x.Kind == "war_started" && Has(notable.SubjectId) && x.SubjectId == notable.SubjectId
                        && x.Sequence <= notable.Sequence);
                    if (origin != null)
                    {
                        string context = PriorConflictContext(journal, origin);
                        if (Has(context)) parts.Add(context);
                    }
                    if (origin != null && origin.Year < events.Min(x => x.Year)) parts.Add(Dated(origin));
                    parts.Add(Dated(notable));
                    var ending = ended.FirstOrDefault(x => Has(notable.SubjectId) && x.SubjectId == notable.SubjectId
                        && x.Sequence != notable.Sequence);
                    if (ending != null)
                        parts.Add("Seu encerramento foi registrado no ano " + N(ending.Year) + ".");
                }
            }
            if (captured.Count > 0)
            {
                var capture = captured.FirstOrDefault(x => Has(x.SubjectName) && Has(x.OtherName));
                if (capture != null)
                {
                    string reign = Has(capture.ActorName) && capture.ActorRole == "ruler_at_event"
                        ? "Durante o reinado de " + capture.ActorName + ", " : string.Empty;
                    parts.Add(reign + capture.OtherName + " conquistou " + capture.SubjectName
                        + (Has(capture.ContextName) ? ", até então pertencente a " + capture.ContextName : string.Empty)
                        + ", no ano " + N(capture.Year) + ".");
                }
            }
            if (destroyed.Count > 0)
                parts.Add("A destruição de " + Quantity(destroyed.Count, "cidade", "cidades")
                    + Examples(destroyed.Select(x => x.SubjectName)) + " também marcou os registros.");
            return string.Join(" ", parts.ToArray());
        }

        private static string PriorConflictContext(ChronicleJournal journal, ChronicleEvent war)
        {
            if (!Has(war.OtherId) || !Has(war.ContextId) || war.OtherId == war.ContextId) return String.Empty;
            var past = journal.Events.Where(x => x != null && Direct(x) && x.Sequence < war.Sequence && x.Year <= war.Year).ToList();
            if (!KingdomType(war.OtherType) || !KingdomType(war.ContextType)) return String.Empty;
            past = past.Where(x => x.Kind == "alliance_left" ? KingdomType(x.SubjectType)
                : x.Kind == "alliance_ended" || (KingdomType(x.OtherType) && KingdomType(x.ContextType))).ToList();
            var alliance = past.LastOrDefault(x => x.Kind == "alliance_formed"
                && ((x.OtherId == war.OtherId && x.ContextId == war.ContextId) || (x.OtherId == war.ContextId && x.ContextId == war.OtherId)));
            if (alliance != null)
            {
                var breakup = past.LastOrDefault(x => (x.Kind == "alliance_ended" && x.SubjectId == alliance.SubjectId)
                    || (x.Kind == "alliance_left" && x.OtherId == alliance.SubjectId && (x.SubjectId == war.OtherId || x.SubjectId == war.ContextId)));
                if (breakup != null && breakup.Sequence > alliance.Sequence)
                    return "Antes desse conflito, " + war.OtherName + " e " + war.ContextName + " haviam integrado a aliança "
                        + alliance.SubjectName + ", formada no ano " + N(alliance.Year) + ". O rompimento foi registrado no ano " + N(breakup.Year) + ".";
            }
            var loss = past.LastOrDefault(x => x.Kind == "city_captured" && Has(x.SubjectName)
                && ((x.OtherId == war.OtherId && x.ContextId == war.ContextId) || (x.OtherId == war.ContextId && x.ContextId == war.OtherId)));
            if (loss != null)
                return "A história entre os dois reinos já incluía a conquista de " + loss.SubjectName + " por " + loss.OtherName
                    + ", até então sob o domínio de " + loss.ContextName + ", no ano " + N(loss.Year) + ".";
            var previous = past.LastOrDefault(x => x.Kind == "war_ended" && Has(x.SubjectName)
                && ((x.OtherId == war.OtherId && x.ContextId == war.ContextId) || (x.OtherId == war.ContextId && x.ContextId == war.OtherId)));
            return previous == null ? String.Empty : "Os dois reinos já haviam participado da guerra " + previous.SubjectName
                + ", encerrada no ano " + N(previous.Year) + ". O novo confronto retomou uma relação já marcada por conflitos.";
        }

        private static string DiscoverySummary(List<ChronicleEvent> events)
        {
            var discoveries = events.Where(x => x.Kind == "discovery" && Has(x.SubjectName)).Take(2).ToList();
            if (discoveries.Count == 0) return string.Empty;
            var text = new StringBuilder();
            foreach (var item in discoveries)
                text.Append(Dated(item)).Append(' ');
            return text.ToString().Trim();
        }

        private static void AddWithinLimit(List<string> paragraphs, string paragraph, int maximumWords)
        {
            if (!Has(paragraph)) return;
            int words = paragraphs.Sum(x => x.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length);
            int additional = paragraph.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
            if (words + additional <= maximumWords) paragraphs.Add(paragraph);
        }

        private static string RelationsSummary(List<ChronicleEvent> events)
        {
            return string.Join(" ", events.Where(x => Direct(x) && (x.Kind == "alliance_formed" || x.Kind == "alliance_ended"
                    || x.Kind == "alliance_joined" || x.Kind == "alliance_left" || x.Kind == "city_transferred"
                    || x.Kind == "name_changed"))
                .Take(3).Select(Dated).ToArray());
        }

        private static string Dated(ChronicleEvent item)
        {
            if (item.Kind == "war_ended" && Direct(item))
                return "No ano " + N(item.Year) + ", a guerra " + (Has(item.SubjectName) ? item.SubjectName : "sem nome registrado") + " chegou ao fim.";
            string sentence = Narrative.DescribeEvent(item);
            string[] openings = { "A ", "O ", "As ", "Os ", "Foi ", "Foram ", "Teve ", "Durante ", "Passou " };
            if (openings.Any(x => sentence.StartsWith(x, StringComparison.Ordinal)))
                sentence = char.ToLower(sentence[0], Portuguese) + sentence.Substring(1);
            return "No ano " + N(item.Year) + ", " + sentence;
        }

        public static string Markdown(ChronicleJournal journal)
        {
            if (journal == null) throw new ArgumentNullException("journal");
            var text = new StringBuilder();
            text.Append("# Livro histórico de ").Append(Md(WorldName(journal))).Append("\n\n")
                .Append("Cada página corresponde a um século de 100 anos. O primeiro cobre os anos 0 a 99 e é encerrado ao chegar ao ano 100.\n\n")
                .Append("A nação dominante é estimada por observações anuais: 50% território, 30% população e 20% exército. ")
                .Append("A média inclui todos os anos observados, com zero nos anos em que uma nação esteve ausente. ")
                .Append("Os períodos sem observação permanecem desconhecidos.\n\n");
            var pages = Pages(journal);
            if (pages.Count == 0) text.Append("O livro ainda não tem páginas.\n");
            foreach (var page in pages)
            {
                text.Append("## Século ").Append(Md(CenturyLabel(page.CenturyIndex))).Append(" — ")
                    .Append(Md(page.Title)).Append("\n\n");
                text.Append("Anos ").Append(N(page.StartYear)).Append(" a ").Append(N(page.EndYear))
                    .Append(" · ").Append(page.IsComplete ? "Concluído" : "Em andamento")
                    .Append(" · ").Append(N(page.ObservedYears)).Append(" de 100 anos observados\n\n");
                foreach (var paragraph in SplitParagraphs(page.Summary)) text.Append(Md(paragraph)).Append("\n\n");
                if (page.Rankings != null && page.Rankings.Count > 0)
                {
                    text.Append("Projeção das nações nos anos observados:\n\n");
                    foreach (var standing in page.Rankings.Take(5))
                        text.Append("- ").Append(Md(standing.Name)).Append(": ")
                            .Append(Points(standing.Score)).Append(" pontos em 100; presente em ")
                            .Append(N(standing.YearsPresent)).Append(" anos observados.\n");
                    text.Append('\n');
                }
                if (Has(page.RecordText))
                    text.Append("### Acontecimentos e nomes deste século\n\n").Append(MdMultiline(page.RecordText)).Append("\n\n");
            }
            return text.ToString();
        }

        public static string Html(ChronicleJournal journal)
        {
            if (journal == null) throw new ArgumentNullException("journal");
            var pages = Pages(journal);
            var text = new StringBuilder();
            text.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>Livro histórico de ").Append(H(WorldName(journal))).Append("</title><style>")
                .Append("*{box-sizing:border-box}body{margin:0;background:#25201b;color:#30291e;font:17px/1.8 Georgia,'Times New Roman',serif}")
                .Append(".shelf{max-width:1000px;margin:auto;padding:32px 20px 60px}.cover{color:#eee1c6;text-align:center;margin:22px auto 28px}")
                .Append(".eyebrow{font:11px/1.5 system-ui,sans-serif;letter-spacing:.18em;text-transform:uppercase;color:#a57b42}")
                .Append(".cover .eyebrow{color:#c8a66e}.cover h1{font-size:clamp(28px,5vw,44px);line-height:1.2;font-weight:normal;margin:12px 0;overflow-wrap:anywhere}")
                .Append(".cover p{font:13px/1.7 system-ui,sans-serif;color:#c2b59c;max-width:650px;margin:auto}")
                .Append(".controls{position:sticky;top:8px;z-index:2;display:flex;gap:10px;align-items:center;justify-content:space-between;background:#342c23eF;border:1px solid #66543c;color:#eee1c6;padding:10px 12px;border-radius:7px;margin-bottom:18px;font:13px/1.4 system-ui,sans-serif}")
                .Append(".controls[hidden]{display:none}button{border:1px solid #9c7b4b;background:#58432b;color:#fff0cf;padding:9px 13px;border-radius:4px;cursor:pointer;font:inherit}button:disabled{opacity:.38;cursor:default}button:focus-visible,a:focus-visible,summary:focus-visible{outline:3px solid #a26b18;outline-offset:3px}")
                .Append(".book-page{scroll-margin-top:90px;background:linear-gradient(90deg,#dbc9a4 0,#f0e4ca 3%,#f6edd8 49%,#eee0c0 100%);border:1px solid #a58a5f;border-radius:4px;box-shadow:0 12px 32px #0005;padding:48px 58px 34px;margin:0 0 28px;min-height:700px;overflow-wrap:anywhere}")
                .Append(".js .book-page{display:none}.js .book-page.is-active{display:block}.book-page h2{font-size:clamp(29px,4vw,40px);line-height:1.2;font-weight:normal;margin:13px 0 20px}.chapter-head{border-bottom:1px solid #cbbb97;padding-bottom:22px;margin-bottom:26px}")
                .Append(".badges{display:flex;flex-wrap:wrap;gap:8px;font:11px/1.5 system-ui,sans-serif}.badge{border:1px solid #bbaa87;border-radius:20px;padding:3px 10px;color:#6b583a}.badge.open{background:#e6d4a8;color:#725019}")
                .Append(".summary p{margin:0 0 20px;text-align:left}.summary p:first-child:first-letter{float:left;font-size:47px;line-height:1;margin:4px 8px 0 0;color:#7c552b}")
                .Append(".leader{border-top:1px solid #cbbb97;border-bottom:1px solid #cbbb97;padding:16px 0;margin:26px 0;display:grid;grid-template-columns:1fr auto;gap:12px;align-items:center}.leader strong{display:block;font-size:22px;font-weight:normal}.label{font:11px/1.5 system-ui,sans-serif;color:#796647;text-transform:uppercase;letter-spacing:.08em}.score{font:24px/1.3 Georgia,serif;text-align:right}.score small{display:block;font:11px/1.5 system-ui,sans-serif;color:#796647}")
                .Append("details{font:13px/1.7 system-ui,sans-serif;margin-top:24px}summary{cursor:pointer;color:#74532b}details p{color:#665740}table{width:100%;border-collapse:collapse;margin-top:14px}th,td{text-align:left;border-bottom:1px solid #d5c5a5;padding:9px 8px 9px 0}th{font-weight:600;color:#705c3e}td:nth-child(2),th:nth-child(2){text-align:right}td:nth-child(3),th:nth-child(3){text-align:right}caption{text-align:left;margin-bottom:8px}.folio{border-top:1px solid #cbbb97;padding-top:18px;margin-top:30px;text-align:center;font:12px/1.5 system-ui,sans-serif;color:#8a7452}")
                .Append(".index{padding:20px 24px;margin:18px 0 26px;background:#322a21;border:1px solid #66543c;border-radius:6px;color:#e1d2b6}.index summary{color:#e1c18f}.index ol{padding-left:24px}.index li{margin:10px 0}.index a{color:#ead7b1;text-decoration:none}.index a:hover{text-decoration:underline}.index small{color:#baa98a}.empty{padding:40px;color:#e1d2b6;text-align:center}.notes{color:#b9ac94;font:12px/1.8 system-ui,sans-serif;text-align:center;margin:24px auto 0;max-width:780px}")
                .Append("@media(max-width:650px){.shelf{padding:14px 10px 35px}.book-page{padding:29px 24px 25px;min-height:600px}.controls{gap:5px;font-size:11px;padding:8px}.controls button{padding:8px}.leader{grid-template-columns:1fr}.score{text-align:left}.cover{margin:15px 8px 22px}th,td{font-size:11px}}")
                .Append("@media print{body{background:white}.shelf{max-width:none;margin:0;padding:0}.cover{color:#30291e}.cover p,.cover .eyebrow{color:#65543a}.controls,.index,.notes{display:none!important}.book-page,.js .book-page{display:block!important;background:white;box-shadow:none;border:0;border-radius:0;padding:20px;min-height:0;break-after:page}.book-page:last-of-type{break-after:auto}details{display:none}.cover{break-after:page}}")
                .Append("</style></head><body><main class=\"shelf\"><header class=\"cover\"><div class=\"eyebrow\">WorldBox · Cronista do Mundo</div><h1>Livro histórico de ")
                .Append(H(WorldName(journal))).Append("</h1><p>Um século em cada página. O passado se fecha a cada cem anos; a página atual acompanha a história em curso.</p></header>")
                .Append("<nav id=\"controls\" class=\"controls\" aria-label=\"Navegação do livro\" hidden><button id=\"previous\" type=\"button\">← Anterior</button><span id=\"page-status\" aria-live=\"polite\"></span><button id=\"show-index\" type=\"button\">Índice</button><button id=\"next\" type=\"button\">Próxima →</button></nav>")
                .Append("<details id=\"book-index\" class=\"index\"><summary>Índice dos séculos</summary><ol>");
            for (int i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                text.Append("<li><a href=\"#century-").Append(N(page.CenturyIndex)).Append("\" data-page=\"")
                    .Append(N(i)).Append("\">Século ").Append(H(CenturyLabel(page.CenturyIndex))).Append(" — ")
                    .Append(H(page.Title)).Append("</a><br><small>Anos ").Append(N(page.StartYear)).Append("–")
                    .Append(N(page.EndYear)).Append(" · ").Append(page.IsComplete ? "Concluído" : "Em andamento").Append("</small></li>");
            }
            text.Append("</ol></details>");
            if (pages.Count == 0) text.Append("<p class=\"empty\">O livro ainda não tem páginas. Carregue um mundo para iniciar a observação.</p>");
            foreach (var page in pages) AppendPage(text, page, journal);
            text.Append("<footer class=\"notes\">O primeiro século cobre os anos 0–99 e se encerra ao chegar ao ano 100. A dominância é um índice calculado sobre os anos observados, ")
                .Append("com pesos de 50% para território, 30% para população e 20% para exército. Anos sem a nação contam como zero na média; anos sem observação não são inventados. ")
                .Append("Páginas concluídas são preservadas no save. Nenhuma conexão de rede é necessária para ler este livro.</footer></main>")
                .Append("<script>(function(){'use strict';var pages=Array.prototype.slice.call(document.querySelectorAll('.book-page'));if(!pages.length)return;")
                .Append("var controls=document.getElementById('controls'),previous=document.getElementById('previous'),next=document.getElementById('next'),status=document.getElementById('page-status'),index=document.getElementById('book-index');var current=0;")
                .Append("function show(n,focus){current=Math.max(0,Math.min(pages.length-1,n));pages.forEach(function(page,i){page.classList.toggle('is-active',i===current);});previous.disabled=current===0;next.disabled=current===pages.length-1;status.textContent='Página '+(current+1)+' de '+pages.length;if(focus){pages[current].focus({preventScroll:true});pages[current].scrollIntoView({block:'start'});}}")
                .Append("previous.addEventListener('click',function(){show(current-1,true);});next.addEventListener('click',function(){show(current+1,true);});document.getElementById('show-index').addEventListener('click',function(){index.open=!index.open;if(index.open)index.scrollIntoView({block:'nearest'});});")
                .Append("Array.prototype.forEach.call(document.querySelectorAll('[data-page]'),function(link){link.addEventListener('click',function(event){event.preventDefault();show(Number(link.getAttribute('data-page')),true);index.open=false;history.replaceState(null,'','#'+pages[current].id);});});")
                .Append("var hash=window.location.hash.slice(1);pages.forEach(function(page,i){if(page.id===hash)current=i;});show(current,false);controls.hidden=false;document.documentElement.classList.add('js');")
                .Append("document.addEventListener('keydown',function(event){if(event.altKey||event.ctrlKey||event.metaKey||event.shiftKey||/INPUT|TEXTAREA|SELECT/.test(document.activeElement.tagName))return;if(event.key==='ArrowRight'){event.preventDefault();show(current+1,true);}else if(event.key==='ArrowLeft'){event.preventDefault();show(current-1,true);}});")
                .Append("})();</script></body></html>");
            return text.ToString();
        }

        private static void AppendPage(StringBuilder text, CenturyPage page, ChronicleJournal journal)
        {
            text.Append("<section class=\"book-page\" id=\"century-").Append(N(page.CenturyIndex))
                .Append("\" tabindex=\"-1\" aria-labelledby=\"title-").Append(N(page.CenturyIndex)).Append("\"><header class=\"chapter-head\"><div class=\"eyebrow\">Século ")
                .Append(H(CenturyLabel(page.CenturyIndex))).Append(" · Anos ").Append(N(page.StartYear)).Append("–").Append(N(page.EndYear))
                .Append("</div><h2 id=\"title-").Append(N(page.CenturyIndex)).Append("\">").Append(H(page.Title)).Append("</h2><div class=\"badges\"><span class=\"badge")
                .Append(page.IsComplete ? "" : " open").Append("\">").Append(page.IsComplete ? "Concluído" : "Em andamento").Append("</span><span class=\"badge\">")
                .Append(N(page.ObservedYears)).Append(" de 100 anos observados</span>");
            if (page.PartialCoverage) text.Append("<span class=\"badge\">Cobertura parcial</span>");
            text.Append("</div></header>");
            var illustrations = BookIllustrations.ForPage(journal, page);
            if (illustrations.Count > 0)
            {
                text.Append("<div style=\"display:flex;justify-content:center;gap:24px;flex-wrap:wrap;margin:22px 0;border-block:1px solid #baa174;padding:18px\">");
                foreach (var illustration in illustrations)
                    text.Append("<figure style=\"margin:0;text-align:center;max-width:160px\"><img width=\"96\" height=\"96\" alt=\"Brasão de ")
                        .Append(H(illustration.NationName)).Append("\" src=\"data:image/png;base64,")
                        .Append(illustration.PngBase64).Append("\"><figcaption>").Append(H(illustration.NationName))
                        .Append("<small style=\"display:block\">Registrado no ano ").Append(N(illustration.Year)).Append("</small></figcaption></figure>");
                text.Append("</div>");
            }
            text.Append("<div class=\"summary\">");
            foreach (var paragraph in SplitParagraphs(page.Summary)) text.Append("<p>").Append(H(paragraph)).Append("</p>");
            text.Append("</div><aside class=\"leader\"><div><span class=\"label\">")
                .Append(Has(page.DominantNationName) && !Has(page.DominantNationId) ? "Liderança compartilhada" : "Nação dominante nos anos observados")
                .Append("</span><strong>").Append(H(Has(page.DominantNationName) ? page.DominantNationName : "Não determinada"))
                .Append("</strong></div><div class=\"score\">").Append(Has(page.DominantNationName) ? Points(page.DominanceScore) : "—")
                .Append("<small>").Append(Has(page.DominantNationName) ? "pontos em 100" : "dados insuficientes").Append("</small></div></aside>");
            if (Has(page.RecordText))
            {
                text.Append("<details class=\"facts\"><summary>Acontecimentos e nomes deste século</summary>")
                    .Append("<p>O resumo destaca as principais transformações. Este registro preserva os nomes de todos os fatos capturados para a página, inclusive nomes editados e suas mudanças.</p>");
                foreach (var paragraph in SplitParagraphs(page.RecordText)) text.Append("<p>").Append(H(paragraph)).Append("</p>");
                text.Append("</details>");
            }
            if (page.Rankings != null && page.Rankings.Count > 0)
            {
                text.Append("<details><summary>Como foi medida a projeção das nações</summary><p>Em cada ano observado, a participação de cada nação no território do mundo pesa 50%, ")
                    .Append("sua participação na população pesa 30% e sua participação no exército pesa 20%. Uma métrica com total zero contribui zero, sem redistribuir seu peso. ")
                    .Append("O índice é a média desses resultados nos ").Append(N(page.ObservedYears)).Append(" anos observados. Se a nação não existia em um desses anos, sua contribuição nesse ano é zero.</p>")
                    .Append("<table><caption>Projeção no período observado</caption><thead><tr><th scope=\"col\">Nação</th><th scope=\"col\">Índice / 100</th><th scope=\"col\">Anos presente</th></tr></thead><tbody>");
                foreach (var standing in page.Rankings)
                {
                    if (standing == null) continue;
                    text.Append("<tr><td>").Append(H(standing.Name)).Append("</td><td>").Append(Points(standing.Score))
                        .Append("</td><td>").Append(N(standing.YearsPresent)).Append("</td></tr>");
                }
                text.Append("</tbody></table></details>");
            }
            text.Append("<div class=\"folio\">Século ").Append(H(CenturyLabel(page.CenturyIndex))).Append(" · ")
                .Append(page.IsComplete ? "Página concluída" : "História acompanhada até o ano " + N(page.ObservedThroughYear)).Append("</div></section>");
        }

        private static int IndexOf(int year) { return (int)Math.Floor(year / 100.0) + 1; }
        private static int ClampYear(long value) { return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value)); }
        private static double Share(int value, double total) { return total > 0 ? Math.Max(0, value) / total : 0; }
        private static bool Direct(ChronicleEvent item) { return item.Evidence == "direct"; }
        private static bool KingdomType(string type) { return !Has(type) || type == "KingdomData"; }
        private static bool Has(string value) { return !string.IsNullOrWhiteSpace(value); }
        private static string N(int value) { return value.ToString(CultureInfo.InvariantCulture); }
        private static string Points(double value) { return (value * 100).ToString("0.0", Portuguese); }
        private static string NationName(NationSnapshot nation) { return Has(nation.Name) ? nation.Name : "Nação " + nation.Id; }
        private static string WorldName(ChronicleJournal journal) { return Has(journal.WorldName) ? journal.WorldName : "um mundo sem nome"; }
        private static List<CenturyPage> Pages(ChronicleJournal journal)
        {
            return (journal.Pages ?? new List<CenturyPage>()).Where(x => x != null).OrderBy(x => x.CenturyIndex).ToList();
        }
        private static List<ChronicleEvent> OfKind(List<ChronicleEvent> events, string kind)
        {
            return events.Where(x => Direct(x) && x.Kind == kind).ToList();
        }
        private static int Count(List<ChronicleEvent> events, string kind)
        {
            return events.Count(x => Direct(x) && x.Kind == kind);
        }
        private static double Standing(Dictionary<string, double> rankings, string id)
        {
            double score;
            return id != null && rankings.TryGetValue(id, out score) ? score : 0;
        }
        private static bool Historical(string kind)
        {
            return kind == "kingdom_founded" || kind == "kingdom_ended" || kind == "city_founded"
                || kind == "city_destroyed" || kind == "city_captured" || kind == "city_transferred"
                || kind == "war_started" || kind == "war_ended" || kind == "alliance_formed"
                || kind == "alliance_ended" || kind == "alliance_joined" || kind == "alliance_left"
                || kind == "rebellion" || kind == "ruler_changed" || kind == "name_changed" || kind == "discovery"
                || kind == "ruler_died" || kind == "ruler_murdered" || kind == "native_history";
        }
        private static string Quantity(int count, string singular, string plural)
        {
            return N(count) + " " + (count == 1 ? singular : plural);
        }
        private static string Examples(IEnumerable<string> names)
        {
            var present = names.Where(Has).Distinct(StringComparer.Ordinal).Take(3).ToList();
            return present.Count == 0 ? string.Empty : " (" + JoinNames(present) + ")";
        }
        private static string JoinNames(List<string> names)
        {
            if (names.Count == 0) return string.Empty;
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.Take(names.Count - 1).ToArray()) + " e " + names[names.Count - 1];
        }
        private static IEnumerable<string> SplitParagraphs(string value)
        {
            return (value ?? string.Empty).Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        }
        private static string CenturyLabel(int index)
        {
            if (index <= 0 || index > 3999) return N(index);
            int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] letters = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var result = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                while (index >= values[i]) { result.Append(letters[i]); index -= values[i]; }
            }
            return result.ToString();
        }
        private static string H(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&#39;");
        }
        private static string Md(string value)
        {
            var encoded = H(value).Replace("\r", " ").Replace("\n", " ");
            var result = new StringBuilder();
            const string special = "\\`*_{}[]()#+-.!|>";
            foreach (char character in encoded)
            {
                if (special.IndexOf(character) >= 0) result.Append('\\');
                result.Append(character);
            }
            return result.ToString();
        }

        private static string MdMultiline(string value)
        {
            return string.Join("\n\n", SplitParagraphs(value).Select(Md).ToArray());
        }
    }
}
