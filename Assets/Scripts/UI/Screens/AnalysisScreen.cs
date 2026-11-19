using PlantDoctor.AI;
using PlantDoctor.AR;
using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// The analysis page. The checklist follows what really happens: the photo is checked and
    /// prepared first, then ONE request to the model identifies the plant and the disease (so
    /// those two lines are active together), then the 3D model and the plan are prepared. No
    /// percentage is shown because the model does not report progress.
    /// </summary>
    public class AnalysisScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.Analysis;

        [SerializeField] Button backButton;
        [SerializeField] TMP_Text headline;
        [SerializeField] TMP_Text caption;
        [SerializeField] AnalysisStepRow[] rows = new AnalysisStepRow[5];

        bool _requestStarted, _answered, _complete;

        protected override void OnBind() => Hook(backButton, () => Ctx.Router.Back());

        protected override void OnShown()
        {
            _requestStarted = _answered = _complete = false;
            GameEvents.OnAIAnalysisStarted += HandleStarted;
            GameEvents.OnAIAnalysisCompleted += HandleCompleted;
            // The demo path starts analysis before this page is visible; read the real state.
            _requestStarted = Ctx.Flow != null && Ctx.Flow.AI != null && Ctx.Flow.AI.IsBusy;
            SetText(headline, "Analysing your plant…");
            SetText(caption, "One request identifies the plant and the disease patterns together.");
            Paint();
        }

        protected override void OnHidden()
        {
            GameEvents.OnAIAnalysisStarted -= HandleStarted;
            GameEvents.OnAIAnalysisCompleted -= HandleCompleted;
        }

        void HandleStarted() { _requestStarted = true; Paint(); }
        void HandleCompleted(PlantAnalysisResult result) { _answered = true; Paint(); }

        /// <summary>The router calls this when the result has arrived and the 3D model and plan are ready.</summary>
        public void MarkComplete()
        {
            _requestStarted = _answered = _complete = true;
            SetText(headline, "Analysis complete");
            Paint();
        }

        public override void Refresh() => Paint();

        void Paint()
        {
            if (Theme == null || rows == null) return;
            // 0 Processing image · 1 Identifying plant · 2 Detecting disease · 3 3D model · 4 Plan
            Set(0, _requestStarted || _answered ? AnalysisStepRow.State.Done : AnalysisStepRow.State.Active);
            var model = _answered ? AnalysisStepRow.State.Done : _requestStarted ? AnalysisStepRow.State.Active : AnalysisStepRow.State.Pending;
            Set(1, model); Set(2, model);
            Set(3, _complete ? AnalysisStepRow.State.Done : _answered ? AnalysisStepRow.State.Active : AnalysisStepRow.State.Pending);
            Set(4, _complete ? AnalysisStepRow.State.Done : AnalysisStepRow.State.Pending);
        }

        void Set(int i, AnalysisStepRow.State s)
        {
            if (i < rows.Length && rows[i] != null) rows[i].Set(Theme, s);
        }
    }
}
