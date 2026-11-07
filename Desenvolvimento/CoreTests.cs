using WorldChronicle;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;

int passed = 0;
var results = new List<string>();

void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
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
ChronicleJournal Journal(string worldName = "São Éldar — 世界") => new ChronicleJournal
{
    WorldId = "f24ac9ab2fbb4e678234d79bf2d4aa56",
    BranchId = "c3f23f5bf2084d68ab09e5c6c11788e5",
    WorldName = worldName, StartedYear = 130
};
ChronicleEvent Event(string kind, int year, string subject = "Velas", string other = null, string context = null, string evidence = "direct") => new ChronicleEvent
{
    Id = Guid.NewGuid().ToString("N"), Kind = kind, SubjectId = "subject:42", SubjectName = subject,
    OtherId = other == null ? null : "other:7", OtherName = other,
    ContextId = context == null ? null : "context:9", ContextName = context,
    Evidence = evidence, Detail = "Documento original — João, 攻城, é/á/ã.",
    WorldTime = year * 61.123456789, Year = year
};
void Add(ChronicleJournal journal, ChronicleEvent item)
{
    item.Sequence = ++journal.LastSequence;
    journal.Events.Add(item);
}
ChronicleJournal One(string kind, string evidence = "direct")
{
    var journal = Journal(); Add(journal, Event(kind, 143, "Velas", "Arven", "Dolmar", evidence)); return journal;
}

Test("compressed roundtrip preserves Unicode and every event field", () => {
    var original = One("city_transferred");
    var encoded = JournalStore.Encode(original);
    var restored = JournalStore.Decode(encoded);
    Assert(encoded.StartsWith("wc1:gzip:"), "Save payload is not compressed");
    Assert(JsonConvert.SerializeObject(original) == JsonConvert.SerializeObject(restored), "Journal changed in roundtrip");
});
Test("old save cannot inherit future events", () => {
    var live = One("war_started");
    var checkpoint = JournalStore.Encode(live);
    Add(live, Event("city_destroyed", 200, "Cidade do Futuro"));
    var restored = JournalStore.Decode(checkpoint);
    Assert(restored.Events.Count == 1 && restored.LastSequence == 1, "Future leaked to old checkpoint");
    Assert(!Narrative.Html(restored).Contains("Cidade do Futuro"), "Future leaked to narrative");
});
Test("loaded journal does not share mutable event references", () => {
    var live = One("war_started"); var restored = JournalStore.Decode(JournalStore.Encode(live));
    live.Events[0].SubjectName = "Futuro";
    Assert(restored.Events[0].SubjectName == "Velas", "Restored event shares reference with live world");
});
Test("two worlds preserve separate identities", () => {
    var a = One("war_started"); var b = One("city_destroyed"); b.WorldId = Guid.NewGuid().ToString("N");
    var aa = JournalStore.Decode(JournalStore.Encode(a)); var bb = JournalStore.Decode(JournalStore.Encode(b));
    Assert(aa.WorldId != bb.WorldId && aa.Events[0].Kind != bb.Events[0].Kind, "Worlds were mixed");
});
Test("duplicate event IDs are rejected", () => {
    var j = One("war_started"); Add(j, Event("war_ended", 150)); j.Events[1].Id = j.Events[0].Id;
    Throws(() => JournalStore.Encode(j));
});
Test("duplicate event sequence is rejected", () => {
    var j = One("war_started"); Add(j, Event("war_ended", 150)); j.Events[1].Sequence = 1;
    Throws(() => JournalStore.Encode(j));
});
Test("out-of-order checkpoint events are rejected", () => {
    var j = One("war_started"); Add(j, Event("war_ended", 150)); j.Events.Reverse();
    Throws(() => JournalStore.Encode(j));
});
Test("incomplete last sequence is rejected", () => {
    var j = One("war_started"); j.LastSequence++;
    Throws(() => JournalStore.Encode(j));
});
Test("unknown schema is rejected without migration", () => {
    var j = One("war_started"); j.SchemaVersion = 99;
    Throws(() => JournalStore.Decode(JsonConvert.SerializeObject(j)));
});
Test("invalid world identifier is rejected", () => {
    var j = One("war_started"); j.WorldId = "../another-world";
    Throws(() => JournalStore.Encode(j));
});
Test("corrupt base64 payload is rejected", () => Throws(() => JournalStore.Decode("wc1:gzip:%%%not-base64%%%")));
Test("non-gzip compressed payload is rejected", () => Throws(() => JournalStore.Decode("wc1:gzip:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("bad gzip")))));
Test("empty save payload is rejected", () => Throws(() => JournalStore.Decode("")));
Test("JSON $type cannot instantiate a foreign type", () => {
    var json = JsonConvert.SerializeObject(One("war_started"));
    var injected = "{\"$type\":\"System.IO.FileInfo, System.Private.CoreLib\"," + json.Substring(1);
    var restored = JournalStore.Decode(injected);
    Assert(restored.GetType() == typeof(ChronicleJournal) && restored.Events[0].GetType() == typeof(ChronicleEvent), "Unexpected type instantiated");
});
Test("event metadata $type is ignored", () => {
    var j = One("war_started"); var json = JsonConvert.SerializeObject(j);
    json = json.Replace("\"Events\":[{", "\"Events\":[{\"$type\":\"System.IO.FileInfo, System.Private.CoreLib\",");
    var restored = JournalStore.Decode(json);
    Assert(restored.Events[0].SubjectName == "Velas", "Typed event payload changed behavior");
});
Test("HTML neutralizes game names, IDs and detail markup", () => {
    var j = One("city_founded"); j.WorldName = "<script>alert('world')</script>";
    j.Events[0].SubjectName = "<img src=x onerror=alert(1)>";
    j.Events[0].SubjectId = "\"><script>alert(2)</script>";
    j.Events[0].Detail = "<script>alert('detail')</script>";
    var html = Narrative.Html(j);
    Assert(!html.Contains("<script>") && !html.Contains("<img "), "Raw markup rendered");
    Assert(html.Contains("&lt;script&gt;") && html.Contains("&lt;img"), "Names missing after escaping");
});
Test("Markdown neutralizes links and raw HTML in names", () => {
    var j = One("city_founded"); j.Events[0].SubjectName = "[Clique](javascript:alert(1)) <script>x</script> *rei*";
    var md = Narrative.Markdown(j);
    Assert(!md.Contains("[Clique](") && !md.Contains("<script>"), "Name became a link or markup");
    Assert(md.Contains("\\[Clique\\]") && md.Contains("\\*rei\\*"), "Literal Markdown delimiters were lost");
});
Test("observed disappearance does not claim city destruction", () => {
    var md = Narrative.Markdown(One("city_destroyed", "observed"));
    Assert(md.Contains("deixou de constar") && !md.Contains("foi destruída"), "Disappearance treated as destruction");
});
Test("observed city appearance does not claim foundation", () => {
    var md = Narrative.Markdown(One("city_founded", "observed"));
    Assert(md.Contains("passou a ser observada") && !md.Contains("foi fundada"), "Appearance treated as foundation");
});
Test("ruler change does not invent death", () => {
    var md = Narrative.Markdown(One("ruler_changed"));
    Assert(md.Contains("passou a governar") && !md.Contains("morreu") && !md.Contains("assassin"), "Ruler change invents death or murder");
});
Test("war closure does not invent victory or a treaty", () => {
    var md = Narrative.Markdown(One("war_ended"));
    Assert(md.Contains("não estabelece vencedor nem termos de paz"), "Closure lost epistemic limit");
    Assert(!md.Contains("venceu") && !md.Contains("assinaram") && !md.Contains("foi celebrado"), "Closure invents outcome");
});
Test("direct capture uses conquest only with direct evidence", () => {
    var direct = Narrative.Markdown(One("city_captured")); var observed = Narrative.Markdown(One("city_captured", "observed"));
    Assert(direct.Contains("conquistou a cidade") && !observed.Contains("conquistou") && observed.Contains("passou ao domínio"), "Capture evidence not respected");
});
Test("detail text cannot become invented causes in prose", () => {
    var j = One("war_started"); j.Events[0].Detail = "POR INVEJA SECRETA assassinou o rival";
    Assert(!Narrative.Markdown(j).Contains("INVEJA"), "Arbitrary detail became causal prose");
    Assert(Narrative.Html(j).Contains("POR INVEJA SECRETA"), "Original evidence missing from audit details");
});
Test("narrative follows sequence despite unsorted input", () => {
    var j = Journal(); Add(j, Event("city_founded", 131, "Primeira")); Add(j, Event("city_founded", 132, "Segunda"));
    j.Events.Reverse(); var md = Narrative.Markdown(j);
    Assert(md.IndexOf("Primeira", StringComparison.Ordinal) < md.IndexOf("Segunda", StringComparison.Ordinal), "Narrative sequence changed");
});
Test("HTML preserves event IDs and timestamps for audit", () => {
    var j = One("war_started"); var e = j.Events[0]; var html = Narrative.Html(j);
    Assert(html.Contains(e.Id) && html.Contains(e.WorldTime.ToString("R", CultureInfo.InvariantCulture)) && html.Contains("<details>"), "Audit evidence missing");
});
Test("chapter boundary at next decade is preserved", () => {
    var j = Journal(); Add(j, Event("city_founded", 139)); Add(j, Event("war_started", 140));
    var md = Narrative.Markdown(j);
    Assert(md.Contains("Anos 130 a 139") && md.Contains("Anos 140 a 149"), "Chapter boundary wrong");
});
Test("empty world can save and render without invented history", () => {
    var j = Journal(); var restored = JournalStore.Decode(JournalStore.Encode(j));
    Assert(restored.Events.Count == 0 && Narrative.Markdown(restored).Contains("Ainda não há acontecimentos"), "Empty world invented events");
});
Test("1000 events survive compression and narrative generation", () => {
    var j = Journal();
    for (int i = 0; i < 1000; i++) Add(j, Event(i % 2 == 0 ? "city_founded" : "war_started", 130 + i / 10, "Registro Único " + i));
    var packed = JournalStore.Encode(j); var restored = JournalStore.Decode(packed);
    Assert(restored.Events.Count == 1000 && restored.LastSequence == 1000, "Bulk history truncated");
    Assert(Narrative.Html(restored).Contains("Registro Único 999"), "Last event missing from HTML");
    Assert(packed.Length < JsonConvert.SerializeObject(j).Length, "Compression ineffective for representative history");
});
Test("nonfinite world times are rejected", () => {
    var j = One("war_started"); j.Events[0].WorldTime = double.NaN;
    Throws(() => JournalStore.Encode(j));
});
Test("unrecognized evidence is rejected", () => {
    var j = One("war_started"); j.Events[0].Evidence = "invented";
    Throws(() => JournalStore.Encode(j));
});

var example = Journal("EXEMPLO FICTÍCIO — Arven");
Add(example, Event("observation_started", 130, example.WorldName, evidence: "observed"));
Add(example, Event("kingdom_founded", 131, "Arven"));
Add(example, Event("kingdom_founded", 132, "Dolmar"));
Add(example, Event("city_founded", 133, "Velas", "Dolmar"));
Add(example, Event("alliance_formed", 138, "Pacto das Duas Coroas"));
Add(example, Event("alliance_joined", 138, "Arven", "Pacto das Duas Coroas"));
Add(example, Event("alliance_joined", 138, "Dolmar", "Pacto das Duas Coroas"));
Add(example, Event("alliance_left", 143, "Arven", "Pacto das Duas Coroas"));
Add(example, Event("alliance_ended", 143, "Pacto das Duas Coroas"));
Add(example, Event("war_started", 145, "Guerra de Velas", "Arven", "Dolmar"));
Add(example, Event("city_captured", 147, "Velas", "Arven", "Dolmar"));
Add(example, Event("ruler_changed", 149, "Arven", "Iria", "Oren"));
var end = Event("war_ended", 151, "Guerra de Velas", "Arven", "Dolmar");
end.Detail = "Evento sintético: encerramento do conflito observado. Não foram registrados vencedor nem termos de paz.";
Add(example, end);
string artifacts = Path.Combine(AppContext.BaseDirectory, "../../..", "artifacts");
Directory.CreateDirectory(artifacts);
File.WriteAllText(Path.Combine(artifacts, "cronica-exemplo.html"), Narrative.Html(example), new UTF8Encoding(false));
File.WriteAllText(Path.Combine(artifacts, "cronica-exemplo.md"), Narrative.Markdown(example), new UTF8Encoding(false));
File.WriteAllText(Path.Combine(artifacts, "eventos-exemplo.json"), JsonConvert.SerializeObject(example, Formatting.Indented), new UTF8Encoding(false));
results.Add($"RESULT {passed}/{results.Count} passed");
results.Add("Scope: standalone .NET 8 core tests with NML's real Newtonsoft.Json; game/Unity hooks and other mods were not executed.");
File.WriteAllLines(Path.Combine(artifacts, "validation-report.txt"), results);
Console.WriteLine(results[results.Count - 2]);
Environment.ExitCode = results.Any(x => x.StartsWith("FAIL ")) ? 1 : 0;
