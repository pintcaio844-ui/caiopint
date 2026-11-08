using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NomesCoerentes;
namespace NeoModLoader.api { public class Declaration {public string FolderPath=Program.ModDirectory;} public abstract class BasicMod<T>{protected abstract void OnModLoad();public Declaration GetDeclaration(){return new Declaration();}protected static void LogInfo(string s){Console.WriteLine(s);}} }
namespace UnityEngine { public class Debug { public static void LogError(string s){throw new Exception(s);} } }
public enum MetaType { None,Subspecies,Family,Language,Culture,Religion,Clan,City,Kingdom,Alliance,War,Plot,Unit,Building,Item,World,Special,Army }
public enum ActorSex {Male,Female,None}
public class ActorAsset {public string id; public string name_template_unit;}
public class Actor {
 public ActorAsset asset; public FakeData data=new FakeData();
 public bool isSexFemale(){return data.sex==ActorSex.Female;} public bool isSexMale(){return data.sex==ActorSex.Male;}
 public void setName(string name,bool track){data.name=name;}
 [MethodImpl(MethodImplOptions.NoInlining)] public void generateNewName(){data.name="VANILLA_CHILD";}
 [MethodImpl(MethodImplOptions.NoInlining)] public string getName(){return data.name;}
 [MethodImpl(MethodImplOptions.NoInlining)] public string generateName(MetaType type,long seed,ActorSex sex){return "VANILLA_CULTURE_OVERRIDE";}
}
public class FakeData { public bool custom_name; public string name="VANILLA"; public ActorSex sex; }
public class Army {public FakeData data=new FakeData(); public string name=""; public void setName(string n,bool custom){name=n;} [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(object k){name="VANILLA";} }
public class Subspecies {public FakeData data=new FakeData(); public string name=""; public void setName(string n,bool custom){name=n;} [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(ActorAsset a,object tile){name="VANILLA";} }
public class NamedMeta {public FakeData data=new FakeData(); public void setName(string name,bool track){data.name=name;} }
public class Kingdom:NamedMeta { [MethodImpl(MethodImplOptions.NoInlining)] public void newCivKingdom(Actor a){data.name="VANILLA_REALM";} }
public class City:NamedMeta { [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(Actor a){data.name="VANILLA_CITY";} }
public class Religion:NamedMeta { [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(Actor a){data.name="VANILLA_RELIGION";} }
public class Culture:NamedMeta { [MethodImpl(MethodImplOptions.NoInlining)] public void createCulture(Actor a,bool ignored){data.name="VANILLA_CULTURE";} }
public class Family:NamedMeta { [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(Actor a){data.name="VANILLA_FAMILY";} }
public class Clan:NamedMeta { [MethodImpl(MethodImplOptions.NoInlining)] public void newClan(Actor a,bool ignored){data.name="VANILLA_CLAN";} }
public class Language:NamedMeta { [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(Actor a){data.name="VANILLA_LANGUAGE";} }
public static class NameGenerator { [MethodImpl(MethodImplOptions.NoInlining)] public static string generateName(Actor a,MetaType m,long n,ActorSex s){return "VANILLA";} [MethodImpl(MethodImplOptions.NoInlining)] public static string getName(string id){return "VANILLA";} [MethodImpl(MethodImplOptions.NoInlining)] public static string generateNameFromTemplate(string id,Actor a){return "VANILLA";} }
public class Program {public static string ModDirectory=Path.GetFullPath("NomesCoerentes");static void Assert(bool ok,string reason){if(!ok)throw new Exception(reason);} public static void Main(){var c=new Catalog(Path.Combine(ModDirectory,"Nomes")); foreach(var race in new[]{"human","elf","orc","dwarf"})foreach(var cat in new[]{"unit","city","kingdom","religion","culture","family","clan","language"}) {var actual=c.Next(cat,race,"female");Assert(!string.IsNullOrWhiteSpace(actual),cat+race);}
foreach(var file in Directory.GetFiles(Path.Combine(ModDirectory,"Nomes"),"*.txt")){var lines=File.ReadAllLines(file);Assert(lines.Length>0,"empty");Assert(lines.Distinct(StringComparer.OrdinalIgnoreCase).Count()==lines.Length,"duplicates");}
c=new Catalog(Path.Combine(ModDirectory,"Nomes"));var expected=File.ReadAllLines(Path.Combine(ModDirectory,"Nomes","city_human.txt"));var seen=new HashSet<string>();string last="";foreach(var ignored in expected){last=c.Next("city","human","none");Assert(seen.Add(last),"repeat before exhaustion");}Assert(c.Next("city","human","none")!=last,"boundary repeat");Assert(c.Next("future_type","human","male")==null,"future metadata changed");
new Main().GetType().GetMethod("OnModLoad",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(new Main(),null);
foreach(var race in new[]{"human","elf","orc","dwarf","dragon","wolf","unknown_creature","dragon_red"})foreach(var cat in new[]{MetaType.Unit,MetaType.City,MetaType.Kingdom,MetaType.Religion,MetaType.Culture,MetaType.Family,MetaType.Clan,MetaType.Language})Assert(NameGenerator.generateName(new Actor{asset=new ActorAsset{id=race}},cat,1,ActorSex.Female)!="VANILLA","patch not active");
Assert(NameGenerator.getName("alliance_name")!="VANILLA","alliance");Assert(NameGenerator.getName("world_name")!="VANILLA","world");Assert(NameGenerator.getName("kingdom_motto")=="VANILLA","motto changed");Assert(NameGenerator.generateNameFromTemplate("war_rebellion",null)!="VANILLA","war");Assert(NameGenerator.generateName(null,MetaType.Unit,1,ActorSex.None)!="VANILLA","null actor");Assert(NameGenerator.generateName(null,MetaType.None,1,ActorSex.None)=="VANILLA","unknown category"); var army=new Army();army.generateName(null);Assert(army.name!="VANILLA","army");army.data.custom_name=true;army.generateName(null);Assert(army.name=="VANILLA","custom army not preserved by prefix");var sub=new Subspecies();sub.generateName(new ActorAsset{id="elf"},null);Assert(sub.name!="VANILLA","subspecies");Assert(NameGenerator.generateNameFromTemplate("book_name_fable",null)!="VANILLA","book");foreach(string race in new[]{"human","elf","dwarf","orc"}) {
 var a=new Actor{asset=new ActorAsset{id="unit_"+race,name_template_unit=race+"_unit"}};
 a.generateNewName(); Assert(c.Contains("unit",race,a.data.name),"birth race pool "+race);
 string stable=a.getName();Assert(a.getName()==stable,"name rerolled "+race);
 a.data=new FakeData{name="FROM_LOADED_SAVE"};Assert(c.Contains("unit",race,a.getName()),"preassigned save name "+race);
 a.data=new FakeData{name="Meu Nome",custom_name=true};Assert(a.getName()=="Meu Nome","manual name overwritten");
 a.data=new FakeData{sex=ActorSex.Female};a.generateNewName();Assert(File.ReadAllLines(Path.Combine(ModDirectory,"Nomes","unit_"+race+"_female.txt")).Contains(a.getName()),"female birth "+race);
 Assert(c.Contains("unit",race,a.generateName(MetaType.Unit,1,ActorSex.Male)),"culture override bypass "+race);
 Assert(c.Contains("city",race,NameGenerator.generateNameFromTemplate(race+"_city",null)),"template bypass "+race);
 var k=new Kingdom();k.newCivKingdom(a);Assert(c.Contains("kingdom",race,k.data.name),"realm race "+race);
 var ct=new City();ct.generateName(a);Assert(c.Contains("city",race,ct.data.name),"city race "+race);
 var rel=new Religion();rel.generateName(a);Assert(c.Contains("religion",race,rel.data.name),"religion race "+race);
 var culture=new Culture();culture.createCulture(a,false);Assert(c.Contains("culture",race,culture.data.name),"culture race "+race);
 var fam=new Family();fam.generateName(a);Assert(c.Contains("family",race,fam.data.name),"family race "+race);
 var clan=new Clan();clan.newClan(a,false);Assert(c.Contains("clan",race,clan.data.name),"clan race "+race);
 var lang=new Language();lang.generateName(a);Assert(c.Contains("language",race,lang.data.name),"language race "+race);
}
Assert(Catalog.Race("scorpion")=="other","scorpion mistaken for orc");Assert(Catalog.Race("unit_dwarves")=="dwarf","plural race alias");
Console.WriteLine("PASS: compilation, all race/category routes, creature fallback, null actor, shuffled bags, boundaries and real Harmony interception on a simulated API. NOT an in-game test.");}}
