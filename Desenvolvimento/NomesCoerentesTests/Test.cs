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
 public ActorAsset asset; public FakeData data=new FakeData(); public Family family; public List<Actor> parents=new List<Actor>();
 public IEnumerable<Actor> getParents(){return parents;}
 [MethodImpl(MethodImplOptions.NoInlining)] public void setParent1(Actor a,bool count){if(!parents.Contains(a))parents.Add(a);}
 [MethodImpl(MethodImplOptions.NoInlining)] public void setParent2(Actor a,bool count){if(!parents.Contains(a))parents.Add(a);}
 [MethodImpl(MethodImplOptions.NoInlining)] public void setFamily(Family f){family=f;}
 public bool isSexFemale(){return data.sex==ActorSex.Female;} public bool isSexMale(){return data.sex==ActorSex.Male;}
 public void setName(string name,bool track){data.name=name;}
 [MethodImpl(MethodImplOptions.NoInlining)] public void generateNewName(){data.name="VANILLA_CHILD";}
 [MethodImpl(MethodImplOptions.NoInlining)] public string getName(){return data.name;}
 [MethodImpl(MethodImplOptions.NoInlining)] public string generateName(MetaType type,long seed,ActorSex sex){return "VANILLA_CULTURE_OVERRIDE";}
}
public class FakeData {
 public bool custom_name; public string name="VANILLA"; public ActorSex sex;
 public Dictionary<string,string> saved=new Dictionary<string,string>();
 public string set(string key,string value){saved[key]=value;return value;}
 public void get(string key,out string value,string fallback){if(!saved.TryGetValue(key,out value))value=fallback;}
}
public class Army {public FakeData data=new FakeData(); public string name=""; public void setName(string n,bool custom){name=n;} [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(object k){name="VANILLA";} }
public class Subspecies {public FakeData data=new FakeData(); public string name=""; public void setName(string n,bool custom){name=n;} [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(ActorAsset a,object tile){name="VANILLA";} }
public class NamedMeta {public FakeData data=new FakeData(); public void setName(string name,bool track){data.name=name;} }
public class Kingdom:NamedMeta { public Actor king; [MethodImpl(MethodImplOptions.NoInlining)] public void newCivKingdom(Actor a){king=a;data.name="VANILLA_REALM";} }
public class City:NamedMeta { public Kingdom kingdom; public Kingdom recipient; public bool locked;
 [MethodImpl(MethodImplOptions.NoInlining)] public void finishCapture(Kingdom attacker){if(!locked)kingdom=recipient??attacker;} [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(Actor a){data.name="VANILLA_CITY";} }
public class Religion:NamedMeta { [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(Actor a){data.name="VANILLA_RELIGION";} }
public class Culture:NamedMeta { [MethodImpl(MethodImplOptions.NoInlining)] public void createCulture(Actor a,bool ignored){data.name="VANILLA_CULTURE";} }
public class Family:NamedMeta {
 [MethodImpl(MethodImplOptions.NoInlining)] public void newFamily(Actor a,Actor b,object tile){data.name="VANILLA_FAMILY";} [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(Actor a){data.name="VANILLA_FAMILY";} }
public class Clan:NamedMeta { [MethodImpl(MethodImplOptions.NoInlining)] public void newClan(Actor a,bool ignored){data.name="VANILLA_CLAN";} }
public class Language:NamedMeta { [MethodImpl(MethodImplOptions.NoInlining)] public void generateName(Actor a){data.name="VANILLA_LANGUAGE";} }
public static class NameGenerator { [MethodImpl(MethodImplOptions.NoInlining)] public static string generateName(Actor a,MetaType m,long n,ActorSex s){return "VANILLA";} [MethodImpl(MethodImplOptions.NoInlining)] public static string getName(string id){return "VANILLA";} [MethodImpl(MethodImplOptions.NoInlining)] public static string generateNameFromTemplate(string id,Actor a){return "VANILLA";} }
public class Program {
 public static string ModDirectory=Path.GetFullPath("NomesCoerentes");
 static void Assert(bool ok,string reason){if(!ok)throw new Exception(reason);}
 static Actor Person(string race,ActorSex sex=ActorSex.Male){return new Actor{asset=new ActorAsset{id="unit_"+race,name_template_unit=race+"_unit"},data=new FakeData{sex=sex}};}
 static string Value(Actor a,string key){string value;a.data.get("nomescoerentes."+key,out value,"");return value;}
 static FakeData Clone(FakeData d){return new FakeData{name=d.name,sex=d.sex,custom_name=d.custom_name,saved=new Dictionary<string,string>(d.saved)};}
 public static void Main(){
  var c=new Catalog(Path.Combine(ModDirectory,"Nomes"));
  foreach(var f in Directory.GetFiles(Path.Combine(ModDirectory,"Nomes"),"*.txt")){var lines=File.ReadAllLines(f);Assert(lines.Length>0,"empty list");Assert(lines.Distinct(StringComparer.OrdinalIgnoreCase).Count()==lines.Length,"duplicate list entry");}
  var cityNames=File.ReadAllLines(Path.Combine(ModDirectory,"Nomes","city_human.txt"));var seen=new HashSet<string>();string last="";
  foreach(var ignored in cityNames){last=c.Next("city","human","none");Assert(seen.Add(last),"early bag repetition");}Assert(c.Next("city","human","none")!=last,"bag boundary repetition");
  new Main().GetType().GetMethod("OnModLoad",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(new Main(),null);
  foreach(var race in new[]{"human","elf","dwarf","orc"}){
   var father=Person(race);father.generateNewName();string full=father.getName();string surname=Value(father,"surname");string given=Value(father,"given_name");
   Assert(c.Contains("unit",race,given)&&c.Contains("family",race,surname),"name and surname pools "+race);
   Assert(full==given+" "+surname,"no family object founder lacks surname "+race);
   Assert(father.getName()==full,"duplicate suffix or reroll "+race);
   var mother=Person(race,ActorSex.Female);mother.generateNewName();Assert(File.ReadAllLines(Path.Combine(ModDirectory,"Nomes","unit_"+race+"_female.txt")).Contains(Value(mother,"given_name")),"female names "+race);
   var child=Person(race);child.generateNewName();string childGiven=Value(child,"given_name");
   child.setParent1(mother,true);Assert(Value(child,"surname")==Value(mother,"surname"),"maternal fallback "+race);
   child.setParent2(father,true);Assert(child.getName()==childGiven+" "+surname,"late paternal link "+race);
   var family=new Family();family.newFamily(mother,father,null);Assert(family.data.name==surname,"vanilla family house name "+race);
   child.setFamily(family);Assert(Value(child,"surname")==surname,"membership changed lineage "+race);
   family.data.name="Outra família";Assert(child.getName()==childGiven+" "+surname,"family label rerolled surname "+race);
   var loaded=new Actor{asset=child.asset,data=Clone(child.data)};Assert(loaded.getName()==childGiven+" "+surname,"reload without living parents "+race);
   var grandchild=Person(race);grandchild.generateNewName();grandchild.setParent1(loaded,true);Assert(Value(grandchild,"surname")==surname,"third generation "+race);
   child.parents.Clear();child.setParent1(mother,true);Assert(Value(child,"surname")==surname,"dead father switched dynasty "+race);
   var realm=new Kingdom();realm.newCivKingdom(father);Assert(realm.data.name=="Reino da Casa "+surname,"founder kingdom "+race);
   string realmName=realm.data.name;realm.king=mother;Assert(realm.data.name==realmName,"ordinary succession renamed realm");
   var town=new City();town.generateName(father);string townName=town.data.name;Assert(c.Contains("city",race,townName),"city naming changed "+race);
   town.kingdom=new Kingdom();town.finishCapture(realm);Assert(realm.data.name=="Reino da Casa "+Value(mother,"surname"),"conqueror king dynasty "+race);Assert(town.data.name==townName,"city renamed on conquest");
   string conquered=realm.data.name;realm.king=father;town.finishCapture(realm);Assert(realm.data.name==conquered,"non-transfer changed dynasty");
   var allied=new Kingdom();allied.newCivKingdom(loaded);allied.king=mother;town.recipient=allied;town.finishCapture(realm);Assert(allied.data.name=="Reino da Casa "+Value(mother,"surname"),"allied recipient not actual realm");
   town.recipient=null;town.locked=true;string locked=realm.data.name;town.finishCapture(realm);Assert(realm.data.name==locked,"aborted capture renamed realm");
   var split=new Kingdom();split.newCivKingdom(grandchild);Assert(split.data.name=="Reino da Casa "+surname,"new split realm founder");
   var savedRealm=new Kingdom{data=Clone(split.data)};Assert(savedRealm.data.saved["nomescoerentes.realm_house"]==surname,"realm dynasty persistence");
   var religion=new Religion();religion.generateName(father);Assert(c.Contains("religion",race,religion.data.name),"religion regression");
   var culture=new Culture();culture.createCulture(father,false);Assert(c.Contains("culture",race,culture.data.name),"culture regression");
   var clan=new Clan();clan.newClan(father,false);Assert(c.Contains("clan",race,clan.data.name),"clan regression");
   var lang=new Language();lang.generateName(father);Assert(c.Contains("language",race,lang.data.name),"language regression");
   Assert(c.Contains("unit",race,father.generateName(MetaType.Unit,1,ActorSex.Male)),"culture override regression");
   Assert(c.Contains("city",race,NameGenerator.generateNameFromTemplate(race+"_city",null)),"direct template regression");
   var custom=Person(race);custom.data.custom_name=true;custom.data.name="Caio Stark";Assert(custom.getName()=="Caio Stark","manual name changed");var manualRealm=new Kingdom();manualRealm.newCivKingdom(custom);Assert(manualRealm.data.name=="Reino da Casa Stark","manual founder surname");
  }
  var animal=Person("wolf");animal.generateNewName();Assert(Value(animal,"surname")=="","animal surname");
  Assert(NameGenerator.getName("alliance_name")!="VANILLA","alliance regression");Assert(NameGenerator.getName("kingdom_motto")=="VANILLA","motto changed");Assert(NameGenerator.getName("world_name")!="VANILLA","world regression");
  Assert(NameGenerator.generateNameFromTemplate("war_rebellion",null)!="VANILLA","war regression");Assert(NameGenerator.generateNameFromTemplate("book_name_fable",null)!="VANILLA","book regression");
  Assert(NameGenerator.generateName(null,MetaType.None,1,ActorSex.None)=="VANILLA","future category changed");Assert(Catalog.Race("scorpion")=="other","scorpion confused with orc");
  Console.WriteLine("PASS: real Harmony interception on simulated API; founders without families, inherited surnames across three generations, late parent links, parent death/reload data, stable names, dynasty kingdoms, actual conquest recipient, unchanged city names, preserved normal succession, and previous naming routes. NOT an in-game test.");
 }
}
