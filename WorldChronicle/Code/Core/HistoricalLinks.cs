using System;
using System.Linq;
using System.Collections.Generic;

namespace WorldChronicle
{
    public static class HistoricalLinks
    {
        public static string Describe(ChronicleJournal journal, CenturyPage page)
        {
            var facts = journal.Events.Where(e => e != null && e.Evidence == "direct" && e.Year <= page.ObservedThroughYear).OrderBy(e => e.Sequence).ToList();
            var paragraphs = new List<string>();
            var rulers = facts.Where(e => e.Kind == "ruler_changed" && !String.IsNullOrEmpty(e.OtherId)
                && (String.IsNullOrEmpty(e.SubjectType) || e.SubjectType == "KingdomData")
                && (String.IsNullOrEmpty(e.OtherType) || e.OtherType == "ActorData")
                && (String.IsNullOrEmpty(page.DominantNationId) || e.SubjectId == page.DominantNationId)).ToList();
            foreach (var start in rulers)
            {
                var end = facts.FirstOrDefault(e => e.Sequence > start.Sequence && e.SubjectId == start.SubjectId
                    && ((e.Kind == "ruler_changed" && e.OtherId != start.OtherId)
                    || ((e.Kind == "ruler_died" || e.Kind == "ruler_murdered") && e.OtherId == start.OtherId) || e.Kind == "kingdom_ended"));
                if (end != null && end.Year < page.StartYear) continue;
                string text = "Em " + start.Year + ", " + start.OtherName + " assumiu o governo de " + start.SubjectName + ". ";
                int last = end == null ? page.ObservedThroughYear : end.Year;
                int duration = Math.Max(0, last - start.Year);
                text += end == null ? "Seu reinado continuava em curso no ano " + last + ", com cerca de " + duration + " anos acompanhados desde a posse. "
                    : "Esse reinado estendeu-se até o ano " + last + ", por cerca de " + duration + " anos. ";
                var wars = facts.Where(e => e.Kind == "war_started" && e.Sequence > start.Sequence && (end == null || e.Sequence < end.Sequence)
                    && (e.OtherId == start.SubjectId || e.ContextId == start.SubjectId)).ToList();
                if (wars.Count > 0) text += "Durante esse governo teve início " + wars[0].SubjectName + ", no ano " + wars[0].Year + ". ";
                if (end != null && (end.Kind == "ruler_died" || end.Kind == "ruler_murdered"))
                    text += end.Kind == "ruler_died" ? start.OtherName + " morreu nesse ano. "
                        : start.OtherName + " foi assassinado nesse ano" + (String.IsNullOrEmpty(end.ContextName) ? ". " : " por " + end.ContextName + ". ");
                if (end != null)
                {
                    var successor = facts.FirstOrDefault(e => e.Sequence >= end.Sequence && e.SubjectId == start.SubjectId
                        && e.Kind == "ruler_changed" && !String.IsNullOrEmpty(e.OtherId) && e.OtherId != start.OtherId);
                    if (successor != null)
                    {
                        bool child = successor.ParentIds != null && successor.ParentIds.Contains(start.OtherId);
                        text += "A sucessão foi registrada em " + successor.Year + ", quando " + successor.OtherName
                            + (child ? ", descendente direto de " + start.OtherName + "," : String.Empty) + " passou a governar o reino.";
                    }
                }
                paragraphs.Add(text.Trim());
                if (paragraphs.Count >= 2) break;
            }
            var conquest = facts.FirstOrDefault(e => e.Kind == "city_captured" && e.Year >= page.StartYear && !String.IsNullOrEmpty(e.SubjectId));
            if (conquest != null)
            {
                var loss = facts.FirstOrDefault(e => e.Sequence > conquest.Sequence && e.SubjectId == conquest.SubjectId
                    && (e.Kind == "city_destroyed" || ((e.Kind == "city_captured" || e.Kind == "city_transferred") && e.OtherId != conquest.OtherId)));
                if (loss != null)
                    paragraphs.Add("A conquista de " + conquest.SubjectName + " por " + conquest.OtherName + ", registrada em " + conquest.Year
                        + ", inaugurou um domínio que durou cerca de " + Math.Max(0, loss.Year - conquest.Year) + " anos. "
                        + (loss.Kind == "city_destroyed" ? "A cidade foi destruída em " + loss.Year + "."
                            : "Em " + loss.Year + ", ela passou ao domínio de " + loss.OtherName + "."));
                else paragraphs.Add("Conquistada por " + conquest.OtherName + " em " + conquest.Year + ", " + conquest.SubjectName
                    + " não teve outra mudança de domínio registrada até " + page.ObservedThroughYear + ": um intervalo acompanhado de cerca de "
                    + Math.Max(0, page.ObservedThroughYear - conquest.Year) + " anos.");
            }
            return String.Join("\n\n", paragraphs.ToArray());
        }
    }
}
