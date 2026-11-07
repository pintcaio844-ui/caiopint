using System;
using System.Globalization;
using System.Text;

namespace WorldChronicle
{
    // All game reads happen on Unity's main thread. No actor-wide polling.
    internal static class GameReader
    {
        internal static MapStats Stats { get { return World.world == null ? null : World.world.map_stats; } }
        internal static int Year { get { return Stats == null ? 0 : Stats.year; } }
        internal static double Time { get { return Stats == null ? 0d : Stats.world_time; } }
        internal static string WorldName
        {
            get { return Stats == null || string.IsNullOrWhiteSpace(Stats.name) ? "Mundo sem nome" : Stats.name; }
        }
        internal static bool IsLoading { get { return World.world == null || Stats == null || Config.worldLoading; } }

        internal static string Summary()
        {
            if (IsLoading) return "O mundo ainda está carregando.";
            var world = World.world;
            var text = new StringBuilder();
            text.Append("Ano ").Append(Year).Append(". ");
            text.Append(world.kingdoms == null ? 0 : world.kingdoms.Count).Append(" reinos; ");
            text.Append(world.cities == null ? 0 : world.cities.Count).Append(" cidades; ");
            text.Append(world.wars == null ? 0 : world.wars.countActiveWars()).Append(" guerras em curso.");
            if (world.kingdoms != null)
            {
                int count = 0;
                foreach (var kingdom in world.kingdoms)
                {
                    if (kingdom == null || !kingdom.isAlive() || kingdom.wild || kingdom.data == null) continue;
                    if (++count > 100) { text.Append(" Outros reinos constam nos dados do mundo."); break; }
                    text.Append("\n").Append(kingdom.name).Append(" (ID ")
                        .Append(kingdom.data.id.ToString(CultureInfo.InvariantCulture)).Append(")");
                    if (kingdom.king != null && kingdom.king.data != null)
                        text.Append(", governado por ").Append(kingdom.king.name);
                    text.Append('.');
                }
            }
            return text.ToString();
        }

        internal static ChronicleEvent Event(string kind, BaseSystemData subject,
            BaseSystemData other = null, BaseSystemData context = null, string detail = null)
        {
            return new ChronicleEvent
            {
                Kind = kind, Evidence = "direct",
                SubjectId = Id(subject), SubjectName = subject == null ? null : subject.name,
                SubjectType = subject == null ? null : subject.GetType().Name,
                OtherId = Id(other), OtherName = other == null ? null : other.name,
                OtherType = other == null ? null : other.GetType().Name,
                ContextId = Id(context), ContextName = context == null ? null : context.name,
                ContextType = context == null ? null : context.GetType().Name,
                Detail = detail
            };
        }

        internal static string Id(BaseSystemData data)
        {
            return data == null ? null : data.id.ToString(CultureInfo.InvariantCulture);
        }

        internal static AnnualObservation ObserveNations()
        {
            if (IsLoading || World.world.kingdoms == null)
                throw new InvalidOperationException("O mundo ainda não está pronto para observar as nações.");
            var observation = new AnnualObservation { Year = Year };
            foreach (var kingdom in World.world.kingdoms)
            {
                if (kingdom == null || kingdom.data == null || !kingdom.isAlive() || kingdom.wild || !kingdom.isCiv()) continue;
                observation.Nations.Add(new NationSnapshot
                {
                    Id = Id(kingdom.data), Name = kingdom.name,
                    Territory = Math.Max(0, kingdom.countZones()),
                    Population = Math.Max(0, kingdom.getPopulationPeople()),
                    Army = Math.Max(0, kingdom.countTotalWarriors())
                });
            }
            return observation;
        }
    }
}
