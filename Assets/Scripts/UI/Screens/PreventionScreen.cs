using System;
using System.Collections;
using System.Collections.Generic;
using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>Prevention measures for this plant and condition, as a checklist the user can tick.</summary>
    public class PreventionScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.Prevention;

        [SerializeField] Button backButton;
        [SerializeField] TMP_Text intro;
        [SerializeField] RectTransform listRoot;
        [SerializeField] ChecklistItemView itemTemplate;
        [SerializeField] TMP_Text progress;
        [SerializeField] Button nextButton;

        protected override void OnBind()
        {
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
            Hook(backButton, () => Ctx.Router.Back());
            Hook(nextButton, () => Ctx.Flow.OpenReport());
        }

        public override void Refresh()
        {
            var plan = Ctx.Flow.Prevention != null ? Ctx.Flow.Prevention.Plan : null;
            Clear(listRoot, itemTemplate.transform);
            var ctx = Ctx.Session;
            string what = ctx != null && !ctx.IsHealthy && ctx.Disease != null ? $"Keep {ctx.Disease.diseaseName.ToLowerInvariant()} from coming back" : "Keep your plant healthy";
            SetText(intro, what + ". Tick each habit as you adopt it.");
            if (plan == null || plan.measures == null) { SetText(progress, ""); return; }

            for (int i = 0; i < plan.measures.Length; i++)
            {
                int index = i;
                var item = Instantiate(itemTemplate, listRoot);
                item.gameObject.SetActive(true);
                item.Bind(Theme, plan.measures[i], Ctx.PreventionDone.Contains(i), () => Toggle(index));
            }
            SetText(progress, $"{Ctx.PreventionDone.Count} of {plan.measures.Length} habits ticked");
        }

        void Toggle(int index)
        {
            if (!Ctx.PreventionDone.Add(index)) Ctx.PreventionDone.Remove(index);
            Ctx.Flow.SetPreventionReviewed(Ctx.PreventionDone.Count);
            Refresh();
        }
    }
}
