using System.Collections.Generic;

namespace WorldChronicle
{
    public sealed class ChronicleEvent
    {
        public long Sequence;
        public string Id;
        public string Kind;
        public string SubjectId;
        public string SubjectName;
        public string SubjectType;
        public string OtherId;
        public string OtherName;
        public string OtherType;
        public string ContextId;
        public string ContextName;
        public string ContextType;
        public string ActorId;
        public string ActorName;
        public string ActorRole;
        public string DiscoveryType;
        public string Detail;
        public string Evidence;
        public double WorldTime;
        public int Year;
    }

    public sealed class ChronicleJournal
    {
        public int SchemaVersion = 1;
        public string WorldId;
        public string WorldName;
        public string BranchId;
        public int StartedYear;
        public long LastSequence;
        public List<ChronicleEvent> Events = new List<ChronicleEvent>();
        public int CurrentYear;
        public int MetricsStartedYear = -1;
        public List<AnnualObservation> Observations = new List<AnnualObservation>();
        public List<CenturyPage> Pages = new List<CenturyPage>();
    }

    public sealed class NationSnapshot
    {
        public string Id;
        public string Name;
        public int Territory;
        public int Population;
        public int Army;
    }

    public sealed class AnnualObservation
    {
        public int Year;
        public List<NationSnapshot> Nations = new List<NationSnapshot>();
    }

    public sealed class NationStanding
    {
        public string Id;
        public string Name;
        public double Score;
        public int YearsPresent;
    }

    public sealed class CenturyPage
    {
        public int CenturyIndex;
        public int StartYear;
        public int EndYear;
        public int ObservedThroughYear;
        public int ObservedYears;
        public bool IsComplete;
        public bool PartialCoverage;
        public string Title;
        public string Summary;
        public string RecordText;
        public string DominantNationId;
        public string DominantNationName;
        public double DominanceScore;
        public List<NationStanding> Rankings = new List<NationStanding>();
    }
}
