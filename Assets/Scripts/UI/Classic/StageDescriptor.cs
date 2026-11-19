using System.Collections.Generic;
using PlantDoctor.Core;

namespace PlantDoctor.UI.Classic
{
    /// <summary>Copy for each stage. Data, so wording changes need no code change elsewhere.</summary>
    public static class StageDescriptor
    {
        public const int TotalUserFacingSteps = 8;

        struct Info
        {
            public int Step;
            public string Title;        // upper-case label (legacy text UI, logs)
            public string Friendly;     // sentence-case title shown in the new UI
            public string Objective;
            public string Instruction;
            public string NextAction;
        }

        static readonly Dictionary<SimState, Info> Map = new Dictionary<SimState, Info>
        {
            { SimState.Welcome,           new Info { Step = 0, Title = "WELCOME",              Friendly = "Plant Doctor",          Objective = "Start a plant health check", Instruction = "Scan a leaf to identify the plant, check its health, and learn how to treat and protect it.", NextAction = "Scan a plant" } },
            { SimState.Scan,              new Info { Step = 1, Title = "SCAN YOUR PLANT",      Friendly = "Scan a leaf",           Objective = "Capture a clear leaf image", Instruction = "Fill the frame with one leaf, in good light, and hold steady.", NextAction = "Scan" } },
            { SimState.ScanRetry,         new Info { Step = 1, Title = "SCAN QUALITY LOW",     Friendly = "Let's try that again",  Objective = "Improve the capture",        Instruction = "Move closer to the leaf and improve the lighting.", NextAction = "Retry" } },
            { SimState.AiAnalysis,        new Info { Step = 1, Title = "ANALYZING PLANT",      Friendly = "Analyzing",             Objective = "Running AI inference",       Instruction = "Please wait while the image is analyzed.", NextAction = "" } },
            { SimState.PlantIdentified,   new Info { Step = 2, Title = "PLANT IDENTIFIED",     Friendly = "Plant identified",      Objective = "Confirm the plant",          Instruction = "Review the identified plant and confidence.", NextAction = "Continue" } },
            { SimState.FeatureAnalysis,   new Info { Step = 3, Title = "PLANT ANALYSIS",       Friendly = "Health check",          Objective = "Compare observed vs optimal", Instruction = "Tap any parameter to learn what it means.", NextAction = "See diagnosis" } },
            { SimState.Diagnosis,         new Info { Step = 4, Title = "DIAGNOSIS",            Friendly = "Diagnosis",             Objective = "Understand the condition",   Instruction = "Review the detected condition and the evidence for it.", NextAction = "Continue" } },
            { SimState.LowConfidence,     new Info { Step = 4, Title = "UNCERTAIN RESULT",     Friendly = "Not sure yet",          Objective = "Improve the evidence",       Instruction = "Rescan the leaf, improve lighting, or consult an agricultural expert.", NextAction = "Rescan" } },
            { SimState.FocusPart,         new Info { Step = 5, Title = "ZOOM IN",              Friendly = "Zoom in",               Objective = "Look closely at the affected part", Instruction = "A magnified leaf shows where the disease appears. Turn it with your finger.", NextAction = "See how it spread" } },
            { SimState.DiseaseSimulation, new Info { Step = 6, Title = "HOW IT PROGRESSES",    Friendly = "How it spread",         Objective = "See the infection develop",  Instruction = "Watch the twin replay how the condition developed on this plant.", NextAction = "Start treatment" } },
            { SimState.TreatmentIntro,    new Info { Step = 7, Title = "TREATMENT",            Friendly = "Treatment plan",        Objective = "Prepare the treatment plan", Instruction = "We will now treat the plant step by step.", NextAction = "Begin" } },
            { SimState.TreatmentStep,     new Info { Step = 7, Title = "TREATMENT STEP",       Friendly = "Treat the plant",       Objective = "Perform each step",          Instruction = "Follow the instruction, then continue.", NextAction = "Do this step" } },
            { SimState.RecoveryTimeline,  new Info { Step = 7, Title = "RECOVERY",             Friendly = "Recovery",              Objective = "Observe recovery over time", Instruction = "Watch the plant respond over the following days.", NextAction = "Prevention tips" } },
            { SimState.Prevention,        new Info { Step = 8, Title = "PREVENTION",           Friendly = "Keep it healthy",       Objective = "Prevent future infection",   Instruction = "Learn measures specific to this condition.", NextAction = "Next tip" } },
            { SimState.FinalReport,       new Info { Step = 8, Title = "PLANT HEALTH REPORT",  Friendly = "Health report",         Objective = "Review the session",         Instruction = "Here is a summary of the diagnosis and recovery.", NextAction = "Scan another" } },
            { SimState.Error,             new Info { Step = 0, Title = "SOMETHING WENT WRONG", Friendly = "Something went wrong",  Objective = "Recover gracefully",         Instruction = "The session could not continue.", NextAction = "Try again" } }
        };

        public static string Header(SimState state)
        {
            if (!Map.TryGetValue(state, out var i)) return state.ToString();
            return i.Step > 0 ? $"STEP {i.Step} / {TotalUserFacingSteps}     {i.Title}" : i.Title;
        }

        public static string Title(SimState s) => Map.TryGetValue(s, out var i) ? i.Title : s.ToString();
        public static string FriendlyTitle(SimState s) => Map.TryGetValue(s, out var i) ? i.Friendly : s.ToString();
        public static string Objective(SimState s) => Map.TryGetValue(s, out var i) ? i.Objective : string.Empty;
        public static string Instruction(SimState s) => Map.TryGetValue(s, out var i) ? i.Instruction : string.Empty;
        public static string NextAction(SimState s) => Map.TryGetValue(s, out var i) ? i.NextAction : "Continue";
        public static int Step(SimState s) => Map.TryGetValue(s, out var i) ? i.Step : 0;
        public static float Progress(SimState s) =>
            Map.TryGetValue(s, out var i) ? (float)i.Step / TotalUserFacingSteps : 0f;
    }
}
