using Verse;

namespace InterstellarOdyssey
{
    public enum InterstellarDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public class InterstellarDiagnosticEntry : IExposable
    {
        public int tick;
        public string category;
        public string title;
        public string message;
        public string details;
        public InterstellarDiagnosticSeverity severity;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref category, "category");
            Scribe_Values.Look(ref title, "title");
            Scribe_Values.Look(ref message, "message");
            Scribe_Values.Look(ref details, "details");
            Scribe_Values.Look(ref severity, "severity", InterstellarDiagnosticSeverity.Info);
        }
    }
}
