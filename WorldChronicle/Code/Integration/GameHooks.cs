using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace WorldChronicle
{
    // Signatures checked against NML's publicized WorldBox 0.51 assembly.
    // Each feature patches independently so one missing target cannot disable every feature.
    internal static class GameHooks
    {
        private static Action<string> _log;
        private static readonly HashSet<string> ReportedErrors = new HashSet<string>();
        private static int _allianceCreationDepth;
        private static int _cultureCreationDepth;
        private static bool _readyPending;
        private static readonly Dictionary<City, State> PendingCityNames = new Dictionary<City, State>();

        internal sealed class State
        {
            internal ChronicleEvent Event;
            internal long PreviousId;
            internal bool Active;
            internal bool Changed;
            internal bool CreationScope;
            internal HashSet<long> Before;
        }

        internal static void Install(Harmony harmony, Action<string> log)
        {
            _log = log;
            Patch(harmony, typeof(MapBox), "clearWorld", Type.EmptyTypes, "Changing", null);
            Patch(harmony, typeof(MapBox), "generateNewMap", Type.EmptyTypes, "Changing", null);
            Patch(harmony, typeof(SaveManager), "loadData", new[] { typeof(SavedMap), typeof(string) }, "Changing", null);
            Patch(harmony, typeof(MapBox), "finishingUpLoading", Type.EmptyTypes, null, "Ready");
            Patch(harmony, typeof(SaveManager), "currentWorldToSavedMap", Type.EmptyTypes, "BeforeSave", null);
            Patch(harmony, typeof(MapBox), "isOverUI", Type.EmptyTypes, null, "OverlayPointerAfter");
            Patch(harmony, typeof(WarManager), "newWar", new[] { typeof(Kingdom), typeof(Kingdom), typeof(WarTypeAsset) }, "WarStartBefore", "WarStartAfter");
            Patch(harmony, typeof(WarManager), "endWar", new[] { typeof(War), typeof(WarWinner) }, "WarEndBefore", "WarEndAfter");
            Patch(harmony, typeof(CityManager), "newCity", new[] { typeof(Kingdom), typeof(TileZone), typeof(Actor) }, "CityNewBefore", "CityNewAfter");
            Patch(harmony, typeof(City), "newCityEvent", new[] { typeof(Actor) }, null, "CityNamedAfter");
            Patch(harmony, typeof(CityManager), "removeObject", new[] { typeof(City) }, "CityEndBefore", "CityEndAfter");
            Patch(harmony, typeof(City), "joinAnotherKingdom", new[] { typeof(Kingdom), typeof(bool), typeof(bool) }, "CityTransferBefore", "CityTransferAfter");
            Patch(harmony, typeof(Kingdom), "newCivKingdom", new[] { typeof(Actor) }, "KingdomNewBefore", "KingdomNewAfter");
            Patch(harmony, typeof(KingdomManager), "removeObject", new[] { typeof(Kingdom) }, "KingdomEndBefore", "KingdomEndAfter");
            Patch(harmony, typeof(Kingdom), "setKing", new[] { typeof(Actor), typeof(bool) }, "KingBefore", "KingAfter");
            Patch(harmony, typeof(Kingdom), "removeKing", Type.EmptyTypes, "KingRemoveBefore", "KingRemoveAfter");
            Patch(harmony, typeof(AllianceManager), "newAlliance", new[] { typeof(Kingdom), typeof(Kingdom) }, "AllianceNewBefore", "AllianceNewAfter", "AllianceNewFinally");
            Patch(harmony, typeof(AllianceManager), "dissolveAlliance", new[] { typeof(Alliance) }, "AllianceEndBefore", "AllianceEndAfter");
            Patch(harmony, typeof(Alliance), "join", new[] { typeof(Kingdom), typeof(bool), typeof(bool) }, "AllianceJoinBefore", "AllianceJoinAfter");
            Patch(harmony, typeof(Alliance), "leave", new[] { typeof(Kingdom), typeof(bool) }, "AllianceLeaveBefore", "AllianceLeaveAfter");
            Patch(harmony, typeof(NanoObject), "setName", new[] { typeof(string), typeof(bool) }, "NameBefore", "NameAfter");
            Patch(harmony, typeof(Culture), "createCulture", new[] { typeof(Actor), typeof(bool) }, "CultureCreationBefore", null, "CultureCreationFinally");
            Patch(harmony, typeof(Culture), "recalcBaseStats", Type.EmptyTypes, null, "CultureTraitsAfter");
            Patch(harmony, typeof(BookManager), "newBook", new[] { typeof(Actor) }, "BookBefore", "BookAfter");
            Patch(harmony, typeof(WorldLogMessageExtensions), "add", new[] { typeof(WorldLogMessage) }, "NativeMessageBefore", null);
            Patch(harmony, typeof(WorldLog), "logKingDead", new[] { typeof(Kingdom), typeof(Actor) }, "KingDeathBefore", null);
            Patch(harmony, typeof(WorldLog), "logKingMurder", new[] { typeof(Kingdom), typeof(Actor), typeof(Actor) }, "KingMurderBefore", null);
        }

        private static void Patch(Harmony harmony, Type type, string name, Type[] parameters,
            string before, string after, string finalizer = null)
        {
            try
            {
                var target = AccessTools.Method(type, name, parameters);
                if (target == null) throw new MissingMethodException(type.Name, name);
                // NML bundles different Harmony revisions. Invoke the available overload
                // to support both the original API and the newer IL-manipulator argument.
                MethodInfo patchMethod = null;
                foreach (var candidate in typeof(Harmony).GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (candidate.Name != "Patch") continue;
                    int count = candidate.GetParameters().Length;
                    if (count == 6) { patchMethod = candidate; break; }
                    if (count == 5) patchMethod = candidate;
                }
                if (patchMethod == null) throw new MissingMethodException("Harmony.Patch");
                var arguments = new object[] { target, Method(before), Method(after), null, Method(finalizer), null };
                if (patchMethod.GetParameters().Length == 5) Array.Resize(ref arguments, 5);
                patchMethod.Invoke(harmony, arguments);
                if (_log != null) _log("Hook ativo: " + type.Name + "." + name);
            }
            catch (Exception ex)
            {
                if (_log != null) _log("Recurso indisponível: " + type.Name + "." + name + ": " + ex.Message);
            }
        }

        private static HarmonyMethod Method(string name)
        {
            return name == null ? null : new HarmonyMethod(typeof(GameHooks), name);
        }

        private static void Guard(string feature, Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                if (ReportedErrors.Add(feature) && _log != null)
                    _log("Falha ao observar " + feature + ": " + ex.Message);
            }
        }

        private static bool CanObserve { get { return ChronicleRuntime.CanRecord && !GameReader.IsLoading; } }
        private static void OverlayPointerAfter(ref bool __result)
        {
            if (ChronicleMod.OverlayBlocksPointer) __result = true;
        }
        private static void Emit(State state)
        {
            if (state != null && state.Active && CanObserve && state.Event != null)
                ChronicleRuntime.Record(state.Event);
        }
        private static void Changing()
        {
            _readyPending = false;
            PendingCityNames.Clear();
            DiscoveryReader.Reset();
            Guard("troca de mundo", () => ChronicleRuntime.OnWorldChanging());
        }
        private static void Ready()
        {
            // finishingUpLoading can still execute inside the last SmoothLoader task.
            // Wait for Config.worldLoading=false before creating the first observation.
            _readyPending = true;
            Pump();
        }
        internal static void Pump()
        {
            Guard("mundo carregado", () =>
            {
                if (_readyPending && !GameReader.IsLoading)
                {
                    _readyPending = false;
                    ChronicleRuntime.OnWorldReady();
                }
                if (CanObserve)
                {
                    DiscoveryReader.Seed();
                    FlushCityNames();
                }
            });
        }
        private static void BeforeSave()
        {
            Guard("persistência", () => { FlushCityNames(); ChronicleRuntime.OnBeforeSave(); });
        }
        private static void FlushCityNames()
        {
            if (PendingCityNames.Count == 0 || !CanObserve) return;
            foreach (var pair in new List<KeyValuePair<City, State>>(PendingCityNames))
                CompleteCityName(pair.Key);
        }
        private static void CompleteCityName(City city)
        {
            State state;
            if (city == null || !PendingCityNames.TryGetValue(city, out state)) return;
            PendingCityNames.Remove(city);
            if (city.data != null && GameReader.Id(city.data) == state.Event.SubjectId)
                state.Event.SubjectName = city.name;
            Emit(state);
        }

        private static void WarStartBefore(WarManager __instance, out State __state)
        {
            State state = null;
            Guard("início de guerra", () => { if (CanObserve) state = new State { Active = true, Before = new HashSet<long>(__instance.dict.Keys) }; });
            __state = state;
        }
        private static void WarStartAfter(WarManager __instance, War __result, State __state)
        {
            Guard("início de guerra", () =>
            {
                if (__state == null || __result == null || __result.data == null ||
                    __state.Before.Contains(__result.data.id) || !__instance.dict.ContainsKey(__result.data.id) || __result.hasEnded()) return;
                __state.Event = GameReader.Event("war_started", __result.data, __result.main_attacker == null ? null : __result.main_attacker.data,
                    __result.main_defender == null ? null : __result.main_defender.data, "war_type=" + __result.data.war_type);
                DiscoveryReader.AttachRuler(__state.Event, __result.main_attacker);
                Emit(__state);
            });
        }
        private static void WarEndBefore(War pWar, out State __state)
        {
            State state = null;
            Guard("fim de guerra", () =>
            {
                if (!CanObserve || pWar == null || pWar.data == null || !pWar.isAlive() || pWar.hasEnded()) return;
                state = new State { Active = true, Event = GameReader.Event("war_ended", pWar.data,
                    pWar.main_attacker == null ? null : pWar.main_attacker.data, pWar.main_defender == null ? null : pWar.main_defender.data) };
            });
            __state = state;
        }
        private static void WarEndAfter(War pWar, State __state)
        {
            Guard("fim de guerra", () =>
            {
                if (__state == null || pWar == null || !pWar.hasEnded()) return;
                __state.Event.Detail = "winner=" + pWar.data.winner + ";dead_attackers=" + pWar.data.dead_attackers + ";dead_defenders=" + pWar.data.dead_defenders;
                Emit(__state);
            });
        }

        private static void CityNewBefore(CityManager __instance, out State __state)
        {
            State state = null;
            Guard("fundação de cidade", () => { if (CanObserve) state = new State { Active = true, Before = new HashSet<long>(__instance.dict.Keys) }; });
            __state = state;
        }
        private static void CityNewAfter(CityManager __instance, City __result, State __state)
        {
            Guard("fundação de cidade", () =>
            {
                if (__state == null || __result == null || __result.data == null || __state.Before.Contains(__result.data.id) ||
                    !__instance.dict.ContainsKey(__result.data.id)) return;
                __state.Event = GameReader.Event("city_founded", __result.data, __result.kingdom == null ? null : __result.kingdom.data);
                // Vanilla assigns the name in City.newCityEvent, after newCity returns.
                if (string.IsNullOrWhiteSpace(__state.Event.SubjectName)) PendingCityNames[__result] = __state;
                else Emit(__state);
            });
        }
        private static void CityNamedAfter(City __instance)
        {
            Guard("nome da cidade fundada", () => CompleteCityName(__instance));
        }
        private static void CityEndBefore(City pObject, out State __state)
        {
            State state = null;
            Guard("destruição de cidade", () =>
            {
                CompleteCityName(pObject);
                if (CanObserve && pObject != null && pObject.data != null && pObject.isAlive())
                    state = new State { Active = true, Event = GameReader.Event("city_destroyed", pObject.data,
                        pObject.kingdom == null ? null : pObject.kingdom.data) };
            });
            __state = state;
        }
        private static void CityEndAfter(City pObject, State __state)
        {
            Guard("destruição de cidade", () => { if (__state != null && pObject != null && !pObject.isAlive()) Emit(__state); });
        }
        private static void CityTransferBefore(City __instance, Kingdom pNewSetKingdom, bool pCaptured, bool pRebellion, out State __state)
        {
            State state = null;
            Guard("mudança de soberania", () =>
            {
                if (!CanObserve || __instance == null || __instance.data == null || __instance.kingdom == null || pNewSetKingdom == null) return;
                state = new State { Active = true, PreviousId = __instance.kingdom.data.id,
                    Event = GameReader.Event(pCaptured ? "city_captured" : "city_transferred", __instance.data, pNewSetKingdom.data,
                        __instance.kingdom.data, "captured=" + pCaptured.ToString().ToLowerInvariant() + ";rebellion=" + pRebellion.ToString().ToLowerInvariant()), Changed = pRebellion };
                if (pCaptured) DiscoveryReader.AttachRuler(state.Event, pNewSetKingdom);
            });
            __state = state;
        }
        private static void CityTransferAfter(City __instance, Kingdom pNewSetKingdom, State __state)
        {
            Guard("mudança de soberania", () =>
            {
                if (__state == null || __instance.kingdom == null || __instance.kingdom.data == null ||
                    __instance.kingdom.data.id == __state.PreviousId || __instance.kingdom != pNewSetKingdom) return;
                if (__state.Changed && CanObserve)
                    ChronicleRuntime.Record(new ChronicleEvent { Kind = "rebellion", Evidence = "direct", SubjectId = __state.Event.SubjectId,
                        SubjectName = __state.Event.SubjectName, SubjectType = __state.Event.SubjectType,
                        OtherId = __state.Event.ContextId, OtherName = __state.Event.ContextName, OtherType = __state.Event.ContextType,
                        Detail = "new_kingdom_id=" + __state.Event.OtherId });
                Emit(__state);
            });
        }

        private static void KingdomNewBefore(Kingdom __instance, out State __state)
        {
            State state = null;
            Guard("fundação de reino", () =>
            {
                if (CanObserve && __instance != null && (__instance.asset == null || !__instance.isCiv()))
                    state = new State { Active = true };
            });
            __state = state;
        }
        private static void KingdomNewAfter(Kingdom __instance, State __state)
        {
            Guard("fundação de reino", () =>
            {
                if (__state == null || __instance.data == null || __instance.asset == null || !__instance.isCiv()) return;
                __state.Event = GameReader.Event("kingdom_founded", __instance.data);
                Emit(__state);
            });
        }
        private static void KingdomEndBefore(Kingdom pKingdom, out State __state)
        {
            State state = null;
            Guard("fim de reino", () =>
            {
                if (CanObserve && pKingdom != null && pKingdom.data != null && pKingdom.isAlive() && !pKingdom.wild)
                    state = new State { Active = true, Event = GameReader.Event("kingdom_ended", pKingdom.data) };
            });
            __state = state;
        }
        private static void KingdomEndAfter(Kingdom pKingdom, State __state)
        {
            Guard("fim de reino", () => { if (__state != null && pKingdom != null && !pKingdom.isAlive()) Emit(__state); });
        }
        private static void KingBefore(Kingdom __instance, bool pFromLoad, out State __state)
        {
            State state = null;
            Guard("sucessão", () =>
            {
                if (CanObserve && !pFromLoad && __instance != null && __instance.data != null && !__instance.wild)
                    state = new State { Active = true, PreviousId = __instance.king == null || __instance.king.data == null ? 0 : __instance.king.data.id,
                        Event = GameReader.Event("ruler_changed", __instance.data, null, __instance.king == null ? null : __instance.king.data) };
            });
            __state = state;
        }
        private static void KingAfter(Kingdom __instance, Actor pActor, State __state)
        {
            Guard("sucessão", () =>
            {
                if (__state == null || __instance.king == null || __instance.king != pActor || pActor.data == null || pActor.data.id == __state.PreviousId) return;
                __state.Event.OtherId = GameReader.Id(pActor.data);
                __state.Event.OtherName = pActor.name;
                __state.Event.ParentIds = new[] { pActor.data.parent_id_1.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    pActor.data.parent_id_2.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                Emit(__state);
            });
        }
        private static void KingRemoveBefore(Kingdom __instance, out State __state)
        {
            State state = null;
            Guard("fim de reinado", () =>
            {
                if (CanObserve && __instance != null && __instance.data != null && __instance.king != null && __instance.king.data != null && !__instance.wild)
                    state = new State { Active = true, Event = GameReader.Event("ruler_changed", __instance.data, null, __instance.king.data) };
            });
            __state = state;
        }
        private static void KingRemoveAfter(Kingdom __instance, State __state)
        {
            Guard("fim de reinado", () => { if (__state != null && __instance.king == null) Emit(__state); });
        }

        private static void AllianceNewBefore(AllianceManager __instance, out State __state)
        {
            State state = null;
            Guard("formação de aliança", () =>
            {
                if (!CanObserve) return;
                state = new State { Active = true, Before = new HashSet<long>(__instance.dict.Keys), CreationScope = true };
                _allianceCreationDepth++;
            });
            __state = state;
        }
        private static void AllianceNewAfter(AllianceManager __instance, Alliance __result, Kingdom pKingdom, Kingdom pKingdom2, State __state)
        {
            Guard("formação de aliança", () =>
            {
                if (__state == null || __result == null || __result.data == null || __state.Before.Contains(__result.data.id) ||
                    !__instance.dict.ContainsKey(__result.data.id)) return;
                __state.Event = GameReader.Event("alliance_formed", __result.data,
                    pKingdom == null ? null : pKingdom.data, pKingdom2 == null ? null : pKingdom2.data);
                Emit(__state);
            });
        }
        private static Exception AllianceNewFinally(Exception __exception, State __state)
        {
            if (__state != null && __state.CreationScope) { _allianceCreationDepth = Math.Max(0, _allianceCreationDepth - 1); __state.CreationScope = false; }
            return __exception;
        }
        private static void AllianceEndBefore(Alliance pAlliance, out State __state)
        {
            State state = null;
            Guard("fim de aliança", () =>
            {
                if (CanObserve && pAlliance != null && pAlliance.data != null && pAlliance.isAlive())
                    state = new State { Active = true, Event = GameReader.Event("alliance_ended", pAlliance.data) };
            });
            __state = state;
        }
        private static void AllianceEndAfter(Alliance pAlliance, State __state)
        {
            Guard("fim de aliança", () => { if (__state != null && pAlliance != null && !pAlliance.isAlive()) Emit(__state); });
        }
        private static void AllianceJoinBefore(Alliance __instance, Kingdom pKingdom, out State __state)
        {
            State state = null;
            Guard("entrada em aliança", () =>
            {
                if (CanObserve && _allianceCreationDepth == 0 && __instance != null && __instance.data != null && pKingdom != null &&
                    pKingdom.data != null && !__instance.hasKingdom(pKingdom))
                    state = new State { Active = true, Event = GameReader.Event("alliance_joined", pKingdom.data, __instance.data) };
            });
            __state = state;
        }
        private static void AllianceJoinAfter(Alliance __instance, Kingdom pKingdom, bool __result, State __state)
        {
            Guard("entrada em aliança", () => { if (__state != null && __result && __instance.hasKingdom(pKingdom)) Emit(__state); });
        }
        private static void AllianceLeaveBefore(Alliance __instance, Kingdom pKingdom, out State __state)
        {
            State state = null;
            Guard("saída de aliança", () =>
            {
                if (CanObserve && __instance != null && __instance.data != null && pKingdom != null && pKingdom.data != null && __instance.hasKingdom(pKingdom))
                    state = new State { Active = true, Event = GameReader.Event("alliance_left", pKingdom.data, __instance.data) };
            });
            __state = state;
        }
        private static void AllianceLeaveAfter(Alliance __instance, Kingdom pKingdom, State __state)
        {
            Guard("saída de aliança", () => { if (__state != null && !__instance.hasKingdom(pKingdom)) Emit(__state); });
        }

        private static void NativeMessageBefore(WorldLogMessage pMessage)
        {
            Guard("histórico nativo", () =>
            {
                if (!CanObserve || pMessage == null) return;
                string text;
                try { text = pMessage.getFormatedText(null); }
                catch (Exception) { text = String.Join(" · ", new[] { pMessage.special1, pMessage.special2, pMessage.special3 }); }
                text = System.Text.RegularExpressions.Regex.Replace(text ?? String.Empty, @"</?(?:color|size|b|i)(?:=[^>]*)?>", String.Empty);
                ChronicleRuntime.Record(new ChronicleEvent { Kind = "native_history", Evidence = "direct", NativeLogType = pMessage.asset_id,
                    SubjectName = text, OtherId = pMessage.kingdom_id.ToString(System.Globalization.CultureInfo.InvariantCulture), OtherType = "KingdomData",
                    ActorId = pMessage.unit_id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Detail = "source=WorldLogMessageExtensions.add;special1=" + pMessage.special1 + ";special2=" + pMessage.special2 + ";special3=" + pMessage.special3 });
            });
        }

        private static void KingDeathBefore(Kingdom pKingdom, Actor pActor)
        {
            Guard("morte de governante", () =>
            {
                if (CanObserve && pKingdom != null && pActor != null)
                    ChronicleRuntime.Record(GameReader.Event("ruler_died", pKingdom.data, pActor.data));
            });
        }

        private static void KingMurderBefore(Kingdom pKingdom, Actor pActor, Actor pAttacker)
        {
            Guard("assassinato de governante", () =>
            {
                if (CanObserve && pKingdom != null && pActor != null)
                    ChronicleRuntime.Record(GameReader.Event("ruler_murdered", pKingdom.data, pActor.data, pAttacker == null ? null : pAttacker.data));
            });
        }

        private static void NameBefore(NanoObject __instance, out State __state)
        {
            State state = null;
            Guard("mudança de nome", () =>
            {
                if (!CanObserve || __instance == null || !__instance.isAlive()) return;
                var data = DiscoveryReader.DataFor(__instance);
                // Initial generation of a name is not a rename.
                if (data == null || data.id <= 0 || string.IsNullOrWhiteSpace(data.name)) return;
                var item = GameReader.Event("name_changed", data);
                item.OtherName = data.name;
                state = new State { Active = true, PreviousId = data.id, Event = item };
            });
            __state = state;
        }
        private static void NameAfter(NanoObject __instance, State __state)
        {
            Guard("mudança de nome", () =>
            {
                if (__state == null) return;
                var data = DiscoveryReader.DataFor(__instance);
                if (data == null || data.id != __state.PreviousId || string.IsNullOrWhiteSpace(data.name) ||
                    string.Equals(data.name, __state.Event.OtherName, StringComparison.Ordinal)) return;
                __state.Event.SubjectName = data.name;
                Emit(__state);
            });
        }

        private static void CultureCreationBefore(out State __state)
        {
            // Default traits at culture creation are a starting state, not discoveries.
            __state = new State { CreationScope = true };
            _cultureCreationDepth++;
        }
        private static Exception CultureCreationFinally(Culture __instance, Exception __exception, State __state)
        {
            if (__state != null && __state.CreationScope)
            {
                _cultureCreationDepth = Math.Max(0, _cultureCreationDepth - 1);
                __state.CreationScope = false;
                Guard("cultura criada", () => DiscoveryReader.Remember(__instance));
            }
            return __exception;
        }
        private static void CultureTraitsAfter(Culture __instance)
        {
            Guard("desenvolvimento cultural", () =>
            {
                if (!CanObserve || _cultureCreationDepth != 0 || __instance == null || __instance.data == null) return;
                foreach (var trait in DiscoveryReader.NewTraits(__instance))
                {
                    var item = new ChronicleEvent
                    {
                        Kind = "discovery", DiscoveryType = "culture_trait", Evidence = "observed",
                        SubjectId = trait.id, SubjectName = DiscoveryReader.NativeName(trait), SubjectType = "CultureTrait",
                        OtherId = GameReader.Id(__instance.data), OtherName = __instance.name, OtherType = "CultureData",
                        Detail = "source=culture_trait_acquired;inventor=not_exposed"
                    };
                    ChronicleRuntime.Record(item);
                }
            });
        }

        private static void BookBefore(BookManager __instance, out State __state)
        {
            State state = null;
            Guard("obra escrita", () =>
            {
                if (CanObserve)
                {
                    DiscoveryReader.Seed();
                    state = new State { Active = true };
                }
            });
            __state = state;
        }
        private static void BookAfter(BookManager __instance, Book __result, Actor pActor, State __state)
        {
            Guard("obra escrita", () =>
            {
                if (__state == null || __result == null || __result.data == null ||
                    !__instance.dict.ContainsKey(__result.data.id) || !DiscoveryReader.FirstBookRecord(__result)) return;
                var author = pActor == null ? null : pActor.data;
                var culture = pActor == null || pActor.culture == null ? null : pActor.culture.data;
                var item = GameReader.Event("discovery", __result.data, culture, author);
                item.DiscoveryType = "book_written";
                item.ActorId = __result.data.author_id > 0 ? __result.data.author_id.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
                item.ActorName = __result.data.author_name;
                item.ActorRole = "author";
                item.Detail = "book_type=" + __result.data.book_type;
                __state.Event = item;
                Emit(__state);
            });
        }
    }
}
