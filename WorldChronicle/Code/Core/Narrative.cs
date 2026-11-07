using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WorldChronicle
{
    /// <summary>
    /// A local narrator: only the structured events enter the prose. In particular,
    /// an observed disappearance is not evidence of a death or a military defeat.
    /// </summary>
    public static class Narrative
    {
        public static string DescribeEvent(ChronicleEvent item)
        {
            if (item == null) throw new ArgumentNullException("item");
            return Sentence(item);
        }

        public static string FactsText(IEnumerable<ChronicleEvent> events)
        {
            var result = new StringBuilder();
            if (events == null) return string.Empty;
            foreach (var item in events.Where(x => x != null).OrderBy(x => x.Sequence))
            {
                string sentence = Sentence(item);
                result.Append("Ano ").Append(Number(item.Year)).Append(" — ").Append(sentence);
                var additional = new[] { item.SubjectName, item.OtherName, item.ContextName, item.ActorName }
                    .Where(x => Has(x) && sentence.IndexOf(x, StringComparison.Ordinal) < 0)
                    .Distinct(StringComparer.Ordinal).ToList();
                if (additional.Count > 0)
                    result.Append(" Outros nomes registrados: ").Append(string.Join("; ", additional.ToArray())).Append('.');
                result.Append(" [Registro ").Append(Number(item.Sequence)).Append("]\n\n");
            }
            return result.ToString();
        }

        public static string PlainText(ChronicleJournal journal)
        {
            RequireJournal(journal);
            var result = new StringBuilder();
            result.Append("Crônicas de ").Append(WorldName(journal)).Append("\n\n");
            result.Append("Registro iniciado no ano ").Append(Number(journal.StartedYear))
                .Append(". A crônica reúne os fatos capturados desde o início da observação.\n\n");
            var chapters = Chapters(journal);
            if (chapters.Count == 0)
                result.Append("Ainda não há acontecimentos registrados.\n");
            foreach (var chapter in chapters)
            {
                result.Append(Period(chapter.StartYear)).Append("\n\n");
                foreach (var paragraph in Paragraphs(chapter.Events))
                {
                    result.Append("Ano ").Append(Number(paragraph[0].Year)).Append(". ");
                    for (var i = 0; i < paragraph.Count; i++)
                    {
                        if (i > 0) result.Append(' ');
                        result.Append(Sentence(paragraph[i]));
                    }
                    result.Append("\n\n");
                }
            }
            return result.ToString();
        }

        public static string Markdown(ChronicleJournal journal)
        {
            RequireJournal(journal);
            var result = new StringBuilder();
            result.Append("# Crônicas de ").Append(Md(WorldName(journal))).Append("\n\n");
            result.Append("Registro iniciado no ano ").Append(Number(journal.StartedYear))
                .Append(". Esta crônica descreve os acontecimentos registrados desde o início da observação; ")
                .Append("não reconstrói o passado que não foi capturado. Datas de mudanças observadas por amostragem indicam quando elas foram constatadas.\n\n");

            var chapters = Chapters(journal);
            if (chapters.Count == 0)
                result.Append("Ainda não há acontecimentos registrados.\n");

            foreach (var chapter in chapters)
            {
                result.Append("## ").Append(Period(chapter.StartYear)).Append("\n\n");
                foreach (var paragraph in Paragraphs(chapter.Events))
                {
                    result.Append("**Ano ").Append(Number(paragraph[0].Year)).Append(".** ");
                    for (var i = 0; i < paragraph.Count; i++)
                    {
                        if (i > 0) result.Append(' ');
                        result.Append(Md(Sentence(paragraph[i])));
                    }
                    result.Append("\n\n");
                }
            }

            result.Append("---\n\n")
                .Append("Um rompimento, uma mudança de governante ou o fim de uma guerra não revela, ")
                .Append("por si só, intenções, mortes, vencedores ou os termos de um acordo. ")
                .Append("O arquivo HTML contém os fatos e suas referências para consulta.\n");
            return result.ToString();
        }

        public static string Html(ChronicleJournal journal)
        {
            RequireJournal(journal);
            var result = new StringBuilder();
            result.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>Crônicas de ").Append(H(WorldName(journal))).Append("</title>")
                .Append("<style>")
                .Append("*{box-sizing:border-box}body{margin:0;background:#171c20;color:#e7e4db;font:17px/1.8 Georgia,serif}")
                .Append("main{max-width:900px;margin:auto;padding:56px 28px 72px}header{border-bottom:1px solid #4c4940;padding-bottom:28px;margin-bottom:38px}")
                .Append(".eyebrow{color:#cbb783;letter-spacing:.15em;font:12px/1.5 system-ui,sans-serif;text-transform:uppercase}")
                .Append("h1{font-size:clamp(32px,6vw,54px);line-height:1.15;font-weight:normal;margin:12px 0 22px;overflow-wrap:anywhere}")
                .Append("h2{font-weight:normal;font-size:29px;color:#dbc794;border-bottom:1px solid #393d3c;padding-bottom:9px;margin:0 0 24px}")
                .Append("section{margin:40px 0}p{margin:0 0 20px;overflow-wrap:anywhere}.muted,footer{color:#aaa99f;font:14px/1.7 system-ui,sans-serif}")
                .Append(".year{color:#d5c18f}a{color:#cbb783}sup{font:11px system-ui,sans-serif;margin-left:3px}")
                .Append("details{background:#20272b;border:1px solid #394044;border-radius:6px;padding:14px 18px;margin:22px 0;font:13px/1.7 system-ui,sans-serif}")
                .Append("summary{cursor:pointer;color:#cfbf96}ol{padding-left:24px}li{padding:12px 0;border-bottom:1px solid #394044;overflow-wrap:anywhere}")
                .Append("li:last-child{border-bottom:0}dl{display:grid;grid-template-columns:110px 1fr;gap:3px 12px;margin:9px 0}dt{color:#aaa99f}dd{margin:0;white-space:pre-wrap}")
                .Append("footer{border-top:1px solid #4c4940;padding-top:22px;margin-top:44px}code{font-size:12px;overflow-wrap:anywhere}")
                .Append("@media(max-width:540px){main{padding:30px 18px}dl{grid-template-columns:1fr;gap:0}dd{margin-bottom:8px}}")
                .Append("@media print{body{background:white;color:#222}main{max-width:none;padding:0}h2,.year,a{color:#51411d}details{background:white}footer,.muted{color:#555}}")
                .Append("</style></head><body><main><header><div class=\"eyebrow\">WorldBox · Cronista do Mundo</div>")
                .Append("<h1>Crônicas de ").Append(H(WorldName(journal))).Append("</h1>")
                .Append("<p class=\"muted\">Registro iniciado no ano ").Append(Number(journal.StartedYear))
                .Append(". Cada capítulo reúne fatos observados neste mundo. Acontecimentos anteriores à instalação que não foram capturados permanecem desconhecidos.</p>")
                .Append("<p class=\"muted\">Mundo: <code>").Append(H(journal.WorldId))
                .Append("</code> · Linha do tempo: <code>").Append(H(journal.BranchId)).Append("</code></p></header>");

            var chapters = Chapters(journal);
            if (chapters.Count == 0)
                result.Append("<p>Ainda não há acontecimentos registrados.</p>");

            var anchorCounter = 0;
            foreach (var chapter in chapters)
            {
                // Anchors use a local ordinal instead of game-provided identifiers. IDs
                // can contain arbitrary characters and can be missing in older journals.
                var anchors = new Dictionary<ChronicleEvent, string>();
                foreach (var item in chapter.Events)
                {
                    if (!anchors.ContainsKey(item))
                        anchors.Add(item, "fact-" + (++anchorCounter).ToString(CultureInfo.InvariantCulture));
                }

                result.Append("<section><h2>").Append(H(Period(chapter.StartYear))).Append("</h2>");
                foreach (var paragraph in Paragraphs(chapter.Events))
                {
                    result.Append("<p><strong class=\"year\">Ano ").Append(Number(paragraph[0].Year)).Append(".</strong> ");
                    for (var i = 0; i < paragraph.Count; i++)
                    {
                        var item = paragraph[i];
                        if (i > 0) result.Append(' ');
                        result.Append(H(Sentence(item))).Append("<sup><a href=\"#")
                            .Append(anchors[item]).Append("\" aria-label=\"Consultar fato ")
                            .Append(Number(item.Sequence)).Append("\">[").Append(Number(item.Sequence)).Append("]</a></sup>");
                    }
                    result.Append("</p>");
                }

                result.Append("<details><summary>Consultar os fatos deste capítulo (")
                    .Append(Number(chapter.Events.Count)).Append(")</summary><ol>");
                var audited = new HashSet<ChronicleEvent>();
                foreach (var item in chapter.Events)
                {
                    if (!audited.Add(item)) continue;
                    result.Append("<li id=\"").Append(anchors[item]).Append("\"><strong>Ano ")
                        .Append(Number(item.Year)).Append(" · Registro ").Append(Number(item.Sequence)).Append("</strong><dl>");
                    Fact(result, "Identificador", item.Id);
                    Fact(result, "Tipo", item.Kind);
                    Fact(result, "Evidência", IsDirect(item) ? "Capturado diretamente" : "Observado entre amostras");
                    Fact(result, "Tempo do mundo", item.WorldTime.ToString("R", CultureInfo.InvariantCulture));
                    Fact(result, "Assunto", Describe(item.SubjectName, item.SubjectId));
                    Fact(result, "Participante", Describe(item.OtherName, item.OtherId));
                    Fact(result, "Contexto", Describe(item.ContextName, item.ContextId));
                    Fact(result, "Personagem", Describe(item.ActorName, item.ActorId));
                    Fact(result, "Papel registrado", item.ActorRole);
                    Fact(result, "Detalhe original", item.Detail);
                    result.Append("</dl></li>");
                }
                result.Append("</ol></details></section>");
            }

            result.Append("<footer>Esta crônica preserva a ordem dos registros. Datas de mudanças observadas por amostragem indicam quando elas foram constatadas. Mudanças observadas entre amostras não estabelecem causas, ")
                .Append("e o fim de uma guerra não comprova paz, vitória ou tratado. Nomes são preservados conforme registrados em cada acontecimento. ")
                .Append("Os detalhes de cada capítulo permitem conferir os fatos usados na narrativa.</footer></main></body></html>");
            return result.ToString();
        }

        private static string Sentence(ChronicleEvent item)
        {
            var subject = Name(item.SubjectName, "uma entidade sem nome registrado");
            var other = Name(item.OtherName, "um participante sem nome registrado");
            var context = Name(item.ContextName, "um participante sem nome registrado");
            var direct = IsDirect(item);
            switch (item.Kind)
            {
                case "observation_started":
                    return "A observação deste mundo começou. Os reinos e cidades que já existiam passaram a compor o cenário inicial da crônica.";
                case "kingdom_founded":
                    return direct ? "Foi fundado o reino de " + subject + "."
                        : "O reino de " + subject + " passou a constar entre os reinos observados.";
                case "kingdom_ended":
                    return direct ? "O reino de " + subject + " deixou de existir."
                        : "O reino de " + subject + " deixou de constar entre os reinos observados; a causa não foi registrada.";
                case "city_founded":
                    if (Has(item.OtherName))
                        return direct ? "A cidade de " + subject + " foi fundada no reino de " + other + "."
                            : "A cidade de " + subject + " passou a ser observada no reino de " + other + ".";
                    return direct ? "Foi fundada a cidade de " + subject + "."
                        : "A cidade de " + subject + " passou a constar entre as cidades observadas.";
                case "city_destroyed":
                    return direct ? "A cidade de " + subject + " foi destruída."
                        : "A cidade de " + subject + " deixou de constar entre as cidades observadas; a causa não foi registrada.";
                case "city_captured":
                    if (direct && Has(item.OtherName))
                        return Reign(item) + other + " conquistou a cidade de " + subject
                            + (Has(item.ContextName) ? ", até então sob o domínio de " + context : string.Empty) + ".";
                    if (direct)
                        return "A cidade de " + subject + " foi conquistada; o novo domínio não foi identificado neste registro.";
                    goto case "city_transferred";
                case "city_transferred":
                    if (Has(item.OtherName) && Has(item.ContextName))
                        return "A cidade de " + subject + " deixou de integrar " + context + " e passou ao domínio de " + other + ".";
                    if (Has(item.OtherName))
                        return "A cidade de " + subject + " passou a integrar " + other + ".";
                    if (Has(item.ContextName))
                        return "A cidade de " + subject + " deixou de integrar " + context + "; seu novo vínculo político não foi registrado.";
                    return "A cidade de " + subject + " apresentou uma mudança de vínculo político, sem identificação dos reinos envolvidos.";
                case "ruler_changed":
                    if (Has(item.OtherName) && Has(item.ContextName))
                        return other + " passou a governar " + subject + ", anteriormente governado por " + context + ".";
                    if (Has(item.OtherName))
                        return other + " passou a governar " + subject + ".";
                    return direct ? "O governante de " + subject + " deixou o cargo; um sucessor não foi identificado neste registro."
                        : "O governante de " + subject + " deixou de ser identificado nas observações; um sucessor não foi identificado neste registro.";
                case "alliance_formed":
                    if (direct && Has(item.OtherName) && Has(item.ContextName))
                        return "A aliança " + subject + " foi formada por " + other + " e " + context + ".";
                    return direct ? "Foi formada a aliança " + subject + "."
                        : "A aliança " + subject + " passou a constar entre as alianças observadas.";
                case "alliance_ended":
                    return direct ? "A aliança " + subject + " foi encerrada."
                        : "A aliança " + subject + " deixou de constar entre as alianças observadas; as circunstâncias não foram registradas.";
                case "alliance_joined":
                    return subject + " passou a integrar a aliança " + other + ".";
                case "alliance_left":
                    return subject + " deixou de integrar a aliança " + other + ".";
                case "war_started":
                    if (Has(item.OtherName) && Has(item.ContextName))
                        return direct ? (Has(Reign(item)) ? Reign(item) + "teve" : "Teve") + " início a guerra " + subject + ", entre " + other + " e " + context + "."
                            : "A guerra " + subject + ", entre " + other + " e " + context + ", passou a constar entre os conflitos observados.";
                    return direct ? "Teve início a guerra " + subject + "."
                        : "A guerra " + subject + " passou a constar entre os conflitos observados.";
                case "war_ended":
                    return direct ? "A guerra " + subject + " foi encerrada; este registro não estabelece vencedor nem termos de paz."
                        : "A guerra " + subject + " deixou de constar entre os conflitos observados; seu desfecho não foi identificado.";
                case "rebellion":
                    if (direct)
                        return Has(item.OtherName) ? subject + " se rebelou contra " + other + "."
                            : "Foi registrada uma rebelião envolvendo " + subject + ".";
                    return "Foi observada uma mudança política envolvendo " + subject + "; uma rebelião não foi confirmada diretamente.";
                case "name_changed":
                    return Has(item.OtherName) ? other + " passou a ser conhecido como " + subject + "."
                        : "Foi registrada uma mudança de nome para " + subject + ".";
                case "discovery":
                    if (item.DiscoveryType == "culture_trait")
                        return Has(item.OtherName) ? "A cultura " + other + " passou a apresentar o conhecimento ou traço " + subject + "."
                            : "Passou a ser observado o conhecimento ou traço " + subject + ".";
                    if (item.DiscoveryType == "book_written")
                        return Has(item.ActorName) && item.ActorRole == "author" ? item.ActorName + " escreveu a obra " + subject + "."
                            : "Foi registrada a obra " + subject + ", sem autoria identificada neste registro.";
                    return Has(item.OtherName) ? "Foi registrado o conhecimento " + subject + " em " + other + "."
                        : "Foi registrado o conhecimento " + subject + ".";
                case "observation_gap":
                    return "Houve uma interrupção na observação. Os acontecimentos desse intervalo não puderam ser reconstituídos com segurança.";
                case "load_branch":
                    return "A crônica foi retomada a partir deste estado salvo do mundo, em uma nova linha do tempo.";
                case "checkpoint":
                    return "Foi preservado um ponto de continuidade da crônica.";
                default:
                    return "Foi registrado um acontecimento envolvendo " + subject + "; os dados originais podem ser consultados no registro de fatos.";
            }
        }

        private static List<Chapter> Chapters(ChronicleJournal journal)
        {
            var result = new List<Chapter>();
            var source = journal.Events ?? new List<ChronicleEvent>();
            Chapter current = null;
            foreach (var item in source.Where(x => x != null).OrderBy(x => x.Sequence))
            {
                var startYear = ((long)Math.Floor(item.Year / 10.0)) * 10;
                if (current == null || current.StartYear != startYear)
                {
                    current = new Chapter { StartYear = startYear };
                    result.Add(current);
                }
                current.Events.Add(item);
            }
            return result;
        }

        private static List<List<ChronicleEvent>> Paragraphs(List<ChronicleEvent> events)
        {
            var result = new List<List<ChronicleEvent>>();
            List<ChronicleEvent> current = null;
            foreach (var item in events)
            {
                if (current == null || current[0].Year != item.Year || current.Count >= 6)
                {
                    current = new List<ChronicleEvent>();
                    result.Add(current);
                }
                current.Add(item);
            }
            return result;
        }

        private static string Period(long year)
        {
            return "Anos " + Number(year) + " a " + Number(year + 9);
        }

        private static string Name(string value, string fallback)
        {
            return Has(value) ? value.Trim() : fallback;
        }

        private static string Reign(ChronicleEvent item)
        {
            return Has(item.ActorName) && item.ActorRole == "ruler_at_event"
                ? "Durante o reinado de " + item.ActorName + ", " : string.Empty;
        }

        private static bool Has(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool IsDirect(ChronicleEvent item)
        {
            return string.Equals(item.Evidence, "direct", StringComparison.Ordinal);
        }

        private static string WorldName(ChronicleJournal journal)
        {
            return Name(journal.WorldName, "um mundo sem nome");
        }

        private static string Describe(string name, string id)
        {
            if (Has(name) && Has(id)) return name + " [" + id + "]";
            if (Has(name)) return name;
            return id;
        }

        private static void Fact(StringBuilder result, string label, string value)
        {
            if (!Has(value)) return;
            result.Append("<dt>").Append(H(label)).Append("</dt><dd>").Append(H(value)).Append("</dd>");
        }

        private static string H(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        private static string Md(string value)
        {
            if (value == null) return string.Empty;
            // Escape raw HTML as well as Markdown delimiters; game names must not
            // become links, images, headings, emphasis or executable HTML.
            var encoded = H(value).Replace("\r", " ").Replace("\n", " ");
            var result = new StringBuilder(encoded.Length);
            const string special = "\\`*_{}[]()#+-.!|>";
            foreach (var character in encoded)
            {
                if (special.IndexOf(character) >= 0) result.Append('\\');
                result.Append(character);
            }
            return result.ToString();
        }

        private static string Number(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static void RequireJournal(ChronicleJournal journal)
        {
            if (journal == null) throw new ArgumentNullException("journal");
        }

        private sealed class Chapter
        {
            public long StartYear;
            public readonly List<ChronicleEvent> Events = new List<ChronicleEvent>();
        }
    }
}
