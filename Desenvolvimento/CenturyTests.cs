using WorldChronicle;
using Newtonsoft.Json;
using System.Text;

int passed = 0;
var results = new List<string>();
void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
void Near(double actual, double expected, string message) => Assert(Math.Abs(actual - expected) < 1e-9, message + $" (actual={actual:R}, expected={expected:R})");
void Test(string name, Action test)
{
    try { test(); passed++; results.Add("PASS " + name); Console.WriteLine("PASS " + name); }
    catch (Exception e) { results.Add("FAIL " + name + ": " + e); Console.WriteLine("FAIL " + name + ": " + e); }
}
void Throws(Action action)
{
    bool threw = false;
    try { action(); } catch { threw = true; }
    Assert(threw, "Expected rejection did not occur");
}
ChronicleJournal Journal(int started = 0, int metrics = 0) => new ChronicleJournal
{
    WorldId = "f24ac9ab2fbb4e678234d79bf2d4aa56", BranchId = "c3f23f5bf2084d68ab09e5c6c11788e5",
    WorldName = "São Éldar — 世界", StartedYear = started, MetricsStartedYear = metrics, CurrentYear = started
};
NationSnapshot Nation(string id, string name, int territory = 1, int population = 1, int army = 1) => new NationSnapshot
    { Id = id, Name = name, Territory = territory, Population = population, Army = army };
void Observe(ChronicleJournal journal, int year, params NationSnapshot[] nations) => journal.Observations.Add(new AnnualObservation { Year = year, Nations = nations.ToList() });
void AddEvent(ChronicleJournal journal, string kind, int year, string subject, string other = null, string context = null)
{
    journal.Events.Add(new ChronicleEvent { Id = Guid.NewGuid().ToString("N"), Sequence = ++journal.LastSequence,
        Kind = kind, Year = year, WorldTime = year * 61.0, SubjectId = "subject:1", SubjectName = subject,
        OtherId = other == null ? null : "other:2", OtherName = other,
        ContextId = context == null ? null : "context:3", ContextName = context,
        Evidence = kind == "observation_started" ? "observed" : "direct" });
}
ChronicleEvent TypedEvent(ChronicleJournal journal, string kind, int year, string subject, string subjectId,
    string subjectType, string other = null, string otherId = null, string otherType = null,
    string context = null, string contextId = null, string contextType = null)
{
    AddEvent(journal, kind, year, subject, other, context);
    var item = journal.Events[journal.Events.Count - 1];
    item.SubjectId = subjectId; item.SubjectType = subjectType;
    item.OtherId = otherId; item.OtherType = otherType;
    item.ContextId = contextId; item.ContextType = contextType;
    return item;
}
ChronicleJournal ExtinctionHistory()
{
    var j = Journal();
    Observe(j, 99, Nation("42", "Arven Antiga"));
    Observe(j, 150, Nation("42", "Arven Nova", 30, 30, 30), Nation("43", "Dolmar", 70, 70, 70));
    TypedEvent(j, "kingdom_founded", 5, "Arven Antiga", "42", "KingdomData");
    TypedEvent(j, "ruler_changed", 65, "Arven Antiga", "42", "KingdomData", "Rainha Éria", "ruler10", "ActorData");
    TypedEvent(j, "war_started", 70, "Guerra das Torres", "war1", "WarData", "Arven Antiga", "42", "KingdomData", "Dolmar", "43", "KingdomData");
    TypedEvent(j, "name_changed", 110, "Arven Nova", "42", "KingdomData", "Arven Antiga");
    var capture = TypedEvent(j, "city_captured", 120, "Velas", "city55", "CityData", "Dolmar", "43", "KingdomData", "Arven Nova", "42", "KingdomData");
    capture.ActorId = "ruler11"; capture.ActorName = "Rei João"; capture.ActorRole = "ruler_at_event";
    TypedEvent(j, "kingdom_ended", 195, "Arven Nova", "42", "KingdomData");
    return j;
}
void Refresh(ChronicleJournal journal, int year) { CenturyBook.Refresh(journal, year); journal.CurrentYear = year; }
CenturyPage Page(ChronicleJournal journal, int index) => journal.Pages.Single(p => p.CenturyIndex == index);
NationStanding Rank(CenturyPage page, string id) => page.Rankings.Single(r => r.Id == id);

Test("year 99 stays in first partial century", () => {
    var j = Journal(); Observe(j, 99, Nation("a", "Arven")); Refresh(j, 99);
    Assert(j.Pages.Count == 1 && Page(j, 1).StartYear == 0 && Page(j, 1).EndYear == 99 && !Page(j, 1).IsComplete, "Wrong first-century boundary");
});
Test("year 100 closes first century and opens second", () => {
    var j = Journal(); Observe(j, 99, Nation("a", "Arven")); Refresh(j, 99); Observe(j, 100, Nation("b", "Dolmar")); Refresh(j, 100);
    Assert(j.Pages.Count == 2 && Page(j, 1).IsComplete && !Page(j, 2).IsComplete, "Century not closed/opened at 100");
    Assert(Page(j, 2).StartYear == 100 && Page(j, 2).EndYear == 199, "Wrong second-century interval");
});
Test("year 199 remains the second partial century", () => {
    var j = Journal(); Observe(j, 199, Nation("a", "Arven")); Refresh(j, 199);
    Assert(j.Pages.Count == 2 && !Page(j, 2).IsComplete && Page(j, 2).ObservedThroughYear == 199, "Wrong year-199 page");
});
Test("year 200 closes second century and opens third", () => {
    var j = Journal(); Observe(j, 99, Nation("a", "Arven")); Observe(j, 199, Nation("b", "Dolmar")); Observe(j, 200, Nation("c", "Círia")); Refresh(j, 200);
    Assert(j.Pages.Count == 3 && Page(j, 1).IsComplete && Page(j, 2).IsComplete && !Page(j, 3).IsComplete, "Wrong century status at 200");
    Assert(Page(j, 3).StartYear == 200 && Page(j, 3).EndYear == 299, "Wrong third-century interval");
});
Test("completed century object is unchanged by future events", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Arven")); Observe(j, 99, Nation("a", "Arven")); AddEvent(j, "city_founded", 20, "Velas", "Arven"); Refresh(j, 100);
    var original = JsonConvert.SerializeObject(Page(j, 1)); var reference = Page(j, 1);
    Observe(j, 150, Nation("b", "Dolmar", 1000, 1000, 1000)); AddEvent(j, "city_destroyed", 150, "PORTO DO FUTURO"); Refresh(j, 200);
    Assert(JsonConvert.SerializeObject(Page(j, 1)) == original, "Completed historical page was rewritten");
    Assert(ReferenceEquals(reference, Page(j, 1)), "Completed page object replaced");
});
Test("future dominance cannot change a past century score", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Arven", 80, 80, 80), Nation("b", "Dolmar", 20, 20, 20)); Refresh(j, 100);
    var oldScore = Page(j, 1).DominanceScore;
    for (int year = 100; year < 200; year++) Observe(j, year, Nation("b", "Dolmar", 999999, 999999, 999999));
    Refresh(j, 200);
    Assert(Page(j, 1).DominantNationId == "a" && Page(j, 2).DominantNationId == "b", "Dominance crossed century boundary");
    Near(Page(j, 1).DominanceScore, oldScore, "Past dominance score changed");
});
Test("current page ends at the observed current year", () => {
    var j = Journal(); Observe(j, 201, Nation("a", "Arven")); Refresh(j, 242);
    Assert(!Page(j, 3).IsComplete && Page(j, 3).ObservedThroughYear == 242, "Current page presented a future end");
});
Test("no metrics produce no invented dominant nation", () => {
    var j = Journal(0, -1); Refresh(j, 100);
    Assert(j.Pages.All(p => string.IsNullOrEmpty(p.DominantNationId) && p.ObservedYears == 0), "A nation dominated without observations");
});
Test("no events cannot be narrated as confirmed peace", () => {
    var j = Journal(0, -1); Refresh(j, 100);
    string all = CenturyBook.Markdown(j).ToLowerInvariant();
    Assert(!all.Contains("viveu em paz") && !all.Contains("reinou a paz") && !all.Contains("século pacífico") && !all.Contains("não houve guerras"), "Absence of records became peace");
});
Test("installation in year 150 does not create years 0 through 99", () => {
    var j = Journal(150, 150); Observe(j, 150, Nation("a", "Arven")); Refresh(j, 150);
    Assert(j.Pages.Count == 1 && Page(j, 2).StartYear == 100 && Page(j, 2).ObservedYears == 1, "Pre-installation century was fabricated");
    Assert(Page(j, 2).PartialCoverage, "Partial observation presented as a full century");
});
Test("late installation century remains partial when it closes", () => {
    var j = Journal(150, 150); for (int year = 150; year <= 199; year++) Observe(j, year, Nation("a", "Arven")); Refresh(j, 200);
    Assert(Page(j, 2).IsComplete && Page(j, 2).PartialCoverage && Page(j, 2).ObservedYears == 50, "Incomplete coverage confused with calendar completeness");
});
Test("all 100 sampled years make complete coverage", () => {
    var j = Journal(); for (int year = 0; year < 100; year++) Observe(j, year, Nation("a", "Arven")); Refresh(j, 100);
    Assert(Page(j, 1).IsComplete && !Page(j, 1).PartialCoverage && Page(j, 1).ObservedYears == 100, "Full observation marked partial");
});
Test("territory population and army use weights 50 30 20", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Território", 50, 0, 0), Nation("b", "População", 0, 100, 0), Nation("c", "Exército", 0, 0, 100)); Refresh(j, 0);
    Near(Rank(Page(j, 1), "a").Score, .5, "Territory weight wrong");
    Near(Rank(Page(j, 1), "b").Score, .3, "Population weight wrong");
    Near(Rank(Page(j, 1), "c").Score, .2, "Army weight wrong");
    Assert(Page(j, 1).DominantNationId == "a", "Highest weighted share did not dominate");
});
Test("normalization occurs separately for every observed year", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Arven", 9, 9, 9), Nation("b", "Dolmar", 1, 1, 1));
    Observe(j, 1, Nation("a", "Arven", 10, 10, 10), Nation("b", "Dolmar", 90, 90, 90)); Refresh(j, 1);
    Near(Rank(Page(j, 1), "a").Score, .5, "Raw totals were pooled instead of yearly normalization");
    Near(Rank(Page(j, 1), "b").Score, .5, "Raw totals were pooled instead of yearly normalization");
});
Test("absent and extinct nations contribute zero in later observed years", () => {
    var j = Journal(); Observe(j, 0, Nation("stable", "Estável"), Nation("ephemeral", "Efêmera"));
    Observe(j, 1, Nation("stable", "Estável")); Observe(j, 2, Nation("stable", "Estável")); Refresh(j, 2);
    Near(Rank(Page(j, 1), "stable").Score, 5d / 6d, "Stable nation average wrong");
    Near(Rank(Page(j, 1), "ephemeral").Score, 1d / 6d, "Extinct nation was averaged only over present years");
    Assert(Rank(Page(j, 1), "ephemeral").YearsPresent == 1 && Rank(Page(j, 1), "stable").YearsPresent == 3, "Presence counts wrong");
});
Test("positive score ties do not choose an arbitrary single nation", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Arven"), Nation("b", "Dolmar")); Refresh(j, 0);
    Assert(string.IsNullOrEmpty(Page(j, 1).DominantNationId), "Tie chose arbitrary ID");
    Assert(Page(j, 1).DominantNationName.Contains("Arven") && Page(j, 1).DominantNationName.Contains("Dolmar"), "Tie failed to identify shared leaders");
});
Test("zero indicators do not establish dominance or fabricated equality", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Arven", 0, 0, 0), Nation("b", "Dolmar", 0, 0, 0)); Refresh(j, 0);
    Assert(string.IsNullOrEmpty(Page(j, 1).DominantNationId) && string.IsNullOrEmpty(Page(j, 1).DominantNationName), "All-zero metrics invented leaders");
    Assert(Page(j, 1).Rankings.All(r => r.Score == 0), "All-zero metrics produced a nonzero score");
});
Test("zero denominators add zero without renormalizing other weights", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Arven", 1, 0, 0)); Refresh(j, 0);
    Near(Rank(Page(j, 1), "a").Score, .5, "Missing metrics inflated remaining weight");
});
Test("empty observed year still counts absence as zero", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Arven")); Observe(j, 1); Refresh(j, 1);
    Near(Rank(Page(j, 1), "a").Score, .5, "Empty observed world was excluded from average");
    Assert(Page(j, 1).ObservedYears == 2, "Observed empty year was lost");
});
Test("missing calendar years are not fabricated as annual samples", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Arven")); Observe(j, 99, Nation("a", "Arven")); Refresh(j, 100);
    Assert(Page(j, 1).ObservedYears == 2 && Page(j, 1).PartialCoverage, "Gap years were silently filled");
    Near(Rank(Page(j, 1), "a").Score, 1, "Score denominator used invented samples");
});
Test("renaming a realm with the same ID keeps one nation", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Arven")); Observe(j, 1, Nation("a", "Nova Arven")); Refresh(j, 1);
    Assert(Page(j, 1).Rankings.Count == 1 && Rank(Page(j, 1), "a").YearsPresent == 2, "Rename created a second nation");
    Assert(Rank(Page(j, 1), "a").Name == "Nova Arven", "Latest observed historical name missing");
});
Test("future rename does not rewrite a closed century name", () => {
    var j = Journal(); Observe(j, 99, Nation("a", "Arven Antiga")); Refresh(j, 100);
    Observe(j, 101, Nation("a", "Arven Nova")); Refresh(j, 101);
    Assert(Page(j, 1).DominantNationName == "Arven Antiga" && Page(j, 2).DominantNationName == "Arven Nova", "Future name replaced earlier history");
});
Test("book HTML escapes Unicode nation names and markup", () => {
    var j = Journal(); j.WorldName = "<script>alert('world')</script> São 世界";
    Observe(j, 0, Nation("a", "<img src=x onerror=alert(1)> Éldar 攻城")); Refresh(j, 0);
    var html = CenturyBook.Html(j);
    Assert(!html.Contains("<script>alert('world')</script>") && !html.Contains("<img src=x"), "Unsafe markup from game names rendered");
    Assert(html.Contains("Éldar 攻城") && html.Contains("&lt;img"), "Unicode or escaped names were lost");
});
Test("book Markdown treats game names as literal text", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "[Clique](javascript:alert(1)) *Éldar*")); Refresh(j, 0);
    var md = CenturyBook.Markdown(j);
    Assert(!md.Contains("[Clique](") && md.Contains("\\[Clique\\]"), "Nation became a Markdown link");
});
Test("save checkpoint before future centuries restores only its book pages", () => {
    var j = Journal(); Observe(j, 99, Nation("a", "Arven")); Refresh(j, 100);
    string checkpoint = JournalStore.Encode(j); string oldPage = JsonConvert.SerializeObject(Page(j, 1));
    Observe(j, 199, Nation("b", "Dolmar")); Refresh(j, 200);
    var restored = JournalStore.Decode(checkpoint);
    Assert(restored.Pages.Count == 2 && restored.CurrentYear == 100 && restored.Observations.All(o => o.Year <= 100), "Future observations or pages leaked into save");
    Assert(JsonConvert.SerializeObject(Page(restored, 1)) == oldPage && !Page(restored, 2).IsComplete, "Old book page status was replaced by future");
});
Test("loaded old save branches independently from the live future book", () => {
    var j = Journal(); Observe(j, 99, Nation("a", "Arven")); Refresh(j, 100); var restored = JournalStore.Decode(JournalStore.Encode(j));
    Observe(j, 150, Nation("b", "Dolmar")); Refresh(j, 151);
    Observe(restored, 150, Nation("c", "Círia")); restored.BranchId = Guid.NewGuid().ToString("N"); Refresh(restored, 151);
    Assert(Page(j, 2).DominantNationId == "b" && Page(restored, 2).DominantNationId == "c", "Timeline branches share mutable pages");
    Assert(j.BranchId != restored.BranchId, "Timeline branch ID did not separate");
});
Test("duplicate annual observations are rejected by the save store", () => {
    var j = Journal(); Observe(j, 1, Nation("a", "Arven")); Observe(j, 1, Nation("b", "Dolmar"));
    Throws(() => JournalStore.Encode(j));
});
Test("duplicate nation IDs in one snapshot are rejected", () => {
    var j = Journal(); Observe(j, 1, Nation("a", "Arven"), Nation("a", "Outra Arven"));
    Throws(() => JournalStore.Encode(j));
});
Test("negative annual indicators are rejected", () => {
    var j = Journal(); Observe(j, 1, Nation("a", "Arven", -1)); Throws(() => JournalStore.Encode(j));
});
Test("invalid page interval is rejected by the save store", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Arven")); Refresh(j, 0); Page(j, 1).EndYear = 100;
    Throws(() => JournalStore.Encode(j));
});
Test("nonfinite ranking score is rejected", () => {
    var j = Journal(); Observe(j, 0, Nation("a", "Arven")); Refresh(j, 0); Page(j, 1).Rankings[0].Score = double.NaN;
    Throws(() => JournalStore.Encode(j));
});
Test("old v0.1 save without book fields loads without invented samples", () => {
    var old = "{\"SchemaVersion\":1,\"WorldId\":\"f24ac9ab2fbb4e678234d79bf2d4aa56\",\"BranchId\":\"c3f23f5bf2084d68ab09e5c6c11788e5\",\"WorldName\":\"Antigo\",\"StartedYear\":150,\"LastSequence\":0,\"Events\":[]}";
    var restored = JournalStore.Decode(old);
    Assert(restored.MetricsStartedYear == -1 && restored.Observations.Count == 0 && restored.Pages.Count == 0, "Legacy save invented statistics");
    Refresh(restored, 170);
    Assert(restored.Pages.Count == 1 && Page(restored, 2).ObservedYears == 0 && string.IsNullOrEmpty(Page(restored, 2).DominantNationId), "Legacy render invented coverage or a leader");
});

Test("extinction arc links foundation in an earlier century by ID", () => {
    var j = ExtinctionHistory(); Refresh(j, 200); var text = Page(j, 2).Summary;
    Assert(text.Contains("Arven Antiga") && text.Contains("Arven Nova") && text.Contains("5") && text.Contains("195"), "Extinction lost earlier foundation or historic name");
});
Test("extinction context preserves earlier war city loss rename and ruler", () => {
    var j = ExtinctionHistory(); Refresh(j, 200); var text = Page(j, 2).Summary;
    Assert(text.Contains("Guerra das Torres") && text.Contains("70") && text.Contains("Velas") && text.Contains("120")
        && text.Contains("Rainha Éria") && text.Contains("65") && text.Contains("110"), "Contextual sequence lost an established fact");
    Assert(!text.Contains("por inveja") && !text.Contains("causou sua queda"), "Context invented a causal explanation");
});
Test("same name with a different realm ID cannot supply a foundation", () => {
    var j = ExtinctionHistory(); var founded = j.Events.Single(e => e.Kind == "kingdom_founded");
    founded.SubjectId = "different-realm"; founded.SubjectName = "FOUNDATION_OF_OTHER_REALM"; Refresh(j, 200);
    Assert(!Page(j, 2).Summary.Contains("FOUNDATION_OF_OTHER_REALM"), "Same-name matching linked a different realm");
});
Test("explicit CityData rename cannot become a kingdom rename by ID collision", () => {
    var j = ExtinctionHistory(); var rename = j.Events.Single(e => e.Kind == "name_changed");
    rename.SubjectType = "CityData"; rename.SubjectName = "CITY_NEW_NAME"; rename.OtherName = "CITY_OLD_NAME"; Refresh(j, 200);
    Assert(!Page(j, 2).Summary.Contains("CITY_OLD_NAME passou a chamar-se CITY_NEW_NAME"), "City rename entered extinction arc as realm rename");
});
Test("explicit city context with colliding ID cannot become lost kingdom territory", () => {
    var j = ExtinctionHistory(); var capture = j.Events.Single(e => e.Kind == "city_captured");
    capture.ContextType = "CityData"; capture.SubjectName = "CITY_CONTEXT_COLLISION"; Refresh(j, 200);
    Assert(!Page(j, 2).Summary.Contains("havia perdido a cidade de CITY_CONTEXT_COLLISION"), "City context ID became a realm loss");
});
Test("explicit city war participant cannot become an extinct kingdom reference", () => {
    var j = ExtinctionHistory(); var war = j.Events.Single(e => e.Kind == "war_started");
    war.OtherType = "CityData"; war.SubjectName = "WAR_CONTEXT_COLLISION"; Refresh(j, 200);
    Assert(!Page(j, 2).Summary.Contains("WAR_CONTEXT_COLLISION"), "Wrongly typed war participant linked to kingdom");
});
Test("wrong entity type cannot supply a realm foundation", () => {
    var j = ExtinctionHistory(); var founder = j.Events.Single(e => e.Kind == "kingdom_founded");
    founder.SubjectType = "CityData"; founder.SubjectName = "FOUNDATION_TYPE_COLLISION"; Refresh(j, 200);
    Assert(!Page(j, 2).Summary.Contains("FOUNDATION_TYPE_COLLISION"), "City entity supplied realm foundation");
});
Test("wrong entity type cannot supply a previous realm ruler", () => {
    var j = ExtinctionHistory(); var ruler = j.Events.Single(e => e.Kind == "ruler_changed");
    ruler.SubjectType = "CityData"; ruler.OtherName = "WRONG_ENTITY_RULER"; Refresh(j, 200);
    Assert(!Page(j, 2).Summary.Contains("WRONG_ENTITY_RULER"), "City entity supplied realm succession context");
});
Test("all captured names survive beyond summary highlight limits", () => {
    var j = Journal();
    for (int i = 0; i < 12; i++)
    {
        var item = TypedEvent(j, "city_captured", i + 1, "Cidade Única " + i, "city" + i, "CityData",
            "Reino Novo " + i, "new" + i, "KingdomData", "Reino Anterior " + i, "old" + i, "KingdomData");
        item.ActorName = "Governante Único " + i; item.ActorId = "actor" + i; item.ActorRole = "ruler_at_event";
    }
    Refresh(j, 100); var text = Page(j, 1).RecordText; var html = CenturyBook.Html(j);
    foreach (var item in j.Events)
        foreach (string name in new[] { item.SubjectName, item.OtherName, item.ContextName, item.ActorName })
            Assert(text.Contains(name) && html.Contains(name), "A recorded name was discarded after highlight limit: " + name);
});
Test("capturing realm ruler is named without invented battlefield command", () => {
    var j = Journal(); var item = TypedEvent(j, "city_captured", 20, "Velas", "c1", "CityData", "Arven", "a", "KingdomData", "Dolmar", "b", "KingdomData");
    item.ActorName = "Rainha Éria"; item.ActorId = "r1"; item.ActorRole = "ruler_at_event"; Refresh(j, 20);
    string text = Narrative.DescribeEvent(item) + " " + Page(j, 1).Summary;
    Assert(text.Contains("Durante o reinado de Rainha Éria") && !text.Contains("comandou") && !text.Contains("liderou as tropas"), "Ruler was promoted to field commander");
});
Test("culture trait observation does not invent an inventor or author", () => {
    var j = Journal(); var item = TypedEvent(j, "discovery", 30, "Navegação Élfica", "trait1", "CultureTrait", "Cultura da Lua", "culture1", "CultureData");
    item.DiscoveryType = "culture_trait"; item.ActorId = "actor1"; item.ActorName = "Suposto Inventor"; item.ActorRole = "ruler_at_event";
    string text = Narrative.DescribeEvent(item); Refresh(j, 30);
    Assert(text.Contains("Cultura da Lua") && text.Contains("Navegação Élfica") && !text.Contains("inventou") && !text.Contains("escreveu") && !text.Contains("Suposto Inventor"), "Culture trait acquired a fabricated inventor");
    Assert(Page(j, 1).RecordText.Contains("Suposto Inventor"), "Recorded actor name disappeared from factual catalog");
});
Test("book writing credits the recorded author when author role is confirmed", () => {
    var item = new ChronicleEvent { Kind = "discovery", DiscoveryType = "book_written", Evidence = "direct", SubjectName = "Tratado das Marés", ActorId = "author7", ActorName = "Tálion", ActorRole = "author" };
    string text = Narrative.DescribeEvent(item);
    Assert(text.Contains("Tálion escreveu a obra Tratado das Marés"), "Recorded author was not credited");
});
Test("unconfirmed book actor cannot be promoted to author", () => {
    var item = new ChronicleEvent { Kind = "discovery", DiscoveryType = "book_written", Evidence = "direct", SubjectName = "Tratado das Marés", ActorId = "king7", ActorName = "Rei Tálion", ActorRole = "ruler_at_event" };
    string text = Narrative.DescribeEvent(item);
    Assert(!text.Contains("Rei Tálion escreveu") && text.Contains("sem autoria identificada"), "Unconfirmed actor was credited as author");
});
Test("edited realm names are preserved verbatim in facts and escaped in exports", () => {
    var j = Journal(); var item = TypedEvent(j, "name_changed", 30, "[Nova República] <Éldar> 世界", "a", "KingdomData", "Arven & Filhos");
    Refresh(j, 30); var facts = Page(j, 1).RecordText;
    Assert(facts.Contains(item.SubjectName) && facts.Contains(item.OtherName), "Edited names were translated or replaced");
    var html = CenturyBook.Html(j); var md = CenturyBook.Markdown(j);
    Assert(html.Contains("&lt;Éldar&gt;") && !html.Contains("<Éldar>"), "Edited HTML-like name was not escaped");
    Assert(md.Contains("\\[Nova República\\]") && md.Contains("世界"), "Edited name was not rendered literally in Markdown");
});

var samplePng = Convert.ToBase64String(File.ReadAllBytes(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../..", "WorldChronicle", "icon.png"))));
Test("historical illustrations survive save restoration", () => {
    var j = Journal(); j.Illustrations.Add(new RealmIllustration {CenturyIndex=1,Year=25,NationId="a",NationName="Reino Ilustrado",PngBase64=samplePng});
    var restored=JournalStore.Decode(JournalStore.Encode(j));
    Assert(restored.Illustrations[0].PngBase64==samplePng,"Image lost on load");
});
Test("illustrations stay in the century where captured", () => {
    var j=Journal();j.Illustrations.Add(new RealmIllustration {CenturyIndex=1,Year=25,NationId="a",NationName="Antigo",PngBase64=samplePng});Refresh(j,150);
    Assert(BookIllustrations.ForPage(j,j.Pages[0]).Count==1 && BookIllustrations.ForPage(j,j.Pages[1]).Count==0,"Future page inherited old illustration");
});
Test("unsafe image content is rejected", () => {
    Assert(!BookIllustrations.IsPng("\" onerror=alert(1)") && !BookIllustrations.IsPng(Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg/>"))),"Non-PNG accepted");
});
Test("illustration captions escape edited realm names", () => {
    var j=Journal();j.Illustrations.Add(new RealmIllustration {CenturyIndex=1,Year=25,NationId="a",NationName="<script>bad</script>",PngBase64=samplePng});Refresh(j,50);
    string html=CenturyBook.Html(j);Assert(html.Contains("data:image/png;base64,") && !html.Contains("<script>bad</script>"),"Unsafe caption");
});
Test("war context uses only preceding recorded alliance breakup", () => {
    var j=Journal();
    TypedEvent(j,"alliance_formed",5,"Pacto Antigo","p","AllianceData","Arven","a","KingdomData","Dolmar","b","KingdomData");
    TypedEvent(j,"alliance_left",20,"Arven","a","KingdomData","Pacto Antigo","p","AllianceData");
    TypedEvent(j,"war_started",25,"Guerra Nova","w","WarData","Arven","a","KingdomData","Dolmar","b","KingdomData");Refresh(j,50);
    Assert(j.Pages[0].Summary.Contains("Pacto Antigo") && j.Pages[0].Summary.Contains("rompimento"),"Recorded context missing");
});
var example = Journal(); example.WorldName = "EXEMPLO FICTÍCIO — Três séculos de Arven e Dolmar";
AddEvent(example, "observation_started", 0, example.WorldName);
TypedEvent(example, "kingdom_founded", 0, "Coroa de Arven", "arven", "KingdomData");
TypedEvent(example, "kingdom_founded", 0, "Dolmar", "dolmar", "KingdomData");
TypedEvent(example, "alliance_formed", 30, "Pacto das Duas Coroas", "pact", "AllianceData", "Coroa de Arven", "arven", "KingdomData", "Dolmar", "dolmar", "KingdomData");
TypedEvent(example, "city_founded", 42, "Velas", "velas", "CityData", "Coroa de Arven", "arven", "KingdomData");
var cultureEvent = TypedEvent(example, "discovery", 44, "Navegação", "navigation", "CultureTrait", "Cultura das Marés", "tides", "CultureData");
cultureEvent.DiscoveryType = "culture_trait"; cultureEvent.Evidence = "observed";
var bookEvent = TypedEvent(example, "discovery", 50, "Atlas das Sete Águas", "atlas", "BookData", "Cultura das Marés", "tides", "CultureData", "Tálion", "talion", "ActorData");
bookEvent.DiscoveryType = "book_written"; bookEvent.ActorId = "talion"; bookEvent.ActorName = "Tálion"; bookEvent.ActorRole = "author";
TypedEvent(example, "ruler_changed", 65, "Coroa de Arven", "arven", "KingdomData", "Rainha Éria", "eria", "ActorData");
TypedEvent(example, "name_changed", 80, "Arven", "arven", "KingdomData", "Coroa de Arven");
TypedEvent(example, "alliance_left", 103, "Dolmar", "dolmar", "KingdomData", "Pacto das Duas Coroas", "pact", "AllianceData");
TypedEvent(example, "ruler_changed", 110, "Dolmar", "dolmar", "KingdomData", "João das Marés", "joao", "ActorData");
var warEvent = TypedEvent(example, "war_started", 115, "Guerra de Velas", "warVelas", "WarData", "Dolmar", "dolmar", "KingdomData", "Arven", "arven", "KingdomData");
warEvent.ActorId = "joao"; warEvent.ActorName = "João das Marés"; warEvent.ActorRole = "ruler_at_event";
var captureEvent = TypedEvent(example, "city_captured", 130, "Velas", "velas", "CityData", "Dolmar", "dolmar", "KingdomData", "Arven", "arven", "KingdomData");
captureEvent.ActorId = "joao"; captureEvent.ActorName = "João das Marés"; captureEvent.ActorRole = "ruler_at_event";
TypedEvent(example, "war_ended", 141, "Guerra de Velas", "warVelas", "WarData", "Dolmar", "dolmar", "KingdomData", "Arven", "arven", "KingdomData");
TypedEvent(example, "ruler_changed", 185, "Dolmar", "dolmar", "KingdomData", "Iria", "iria", "ActorData", "João das Marés", "joao", "ActorData");
TypedEvent(example, "kingdom_ended", 195, "Arven", "arven", "KingdomData");
TypedEvent(example, "rebellion", 218, "Lúmen", "cityLumen", "CityData", "Dolmar", "dolmar", "KingdomData");
TypedEvent(example, "kingdom_founded", 218, "Lúmen", "lumen", "KingdomData");
for (int year = 0; year <= 249; year++)
{
    if (year < 100) Observe(example, year, Nation("arven", year < 80 ? "Coroa de Arven" : "Arven", 75, 70, 65), Nation("dolmar", "Dolmar", 25, 30, 35));
    else if (year < 195) Observe(example, year, Nation("arven", "Arven", 25, 30, 35), Nation("dolmar", "Dolmar", 75, 70, 65));
    else if (year < 218) Observe(example, year, Nation("dolmar", "Dolmar", 100, 100, 100));
    else Observe(example, year, Nation("dolmar", "Dolmar", 10, 10, 10), Nation("lumen", "Lúmen", 90, 90, 90));
}
Refresh(example, 249);
JournalStore.Validate(example);
var artifacts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../..", "artifacts"));
Directory.CreateDirectory(artifacts);
File.WriteAllText(Path.Combine(artifacts, "livro-exemplo.html"), CenturyBook.Html(example), new UTF8Encoding(false));
File.WriteAllText(Path.Combine(artifacts, "livro-exemplo.md"), CenturyBook.Markdown(example), new UTF8Encoding(false));
File.WriteAllText(Path.Combine(artifacts, "livro-exemplo.json"), JsonConvert.SerializeObject(example, Formatting.Indented), new UTF8Encoding(false));
results.Add($"RESULT {passed}/{results.Count} passed");
foreach (var path in new[] { "CenturyBook.cs", "Narrative.cs", "Models.cs", "JournalStore.cs" }.Select(name => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../..", "WorldChronicle", "Code", "Core", name))))
    results.Add("SHA256 " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() + " " + path);
results.Add("Scope: .NET8 unit/behavior tests of century aggregation, save checkpoints and book rendering. Example data is entirely fictional.");
results.Add("Limitations: no WorldBox/Unity execution, no empirical timing validation of yearly sampling, and no proof of coexistence with subscribed mods. Manual time reversal is handled by the runtime; tested rollback uses an old saved checkpoint.");
File.WriteAllLines(Path.Combine(artifacts, "century-validation-report.txt"), results);
Console.WriteLine(results.Single(x => x.StartsWith("RESULT ")));
Environment.ExitCode = results.Any(x => x.StartsWith("FAIL ")) ? 1 : 0;
