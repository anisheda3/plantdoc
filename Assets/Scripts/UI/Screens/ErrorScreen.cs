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
    /// <summary>Something went wrong that the current page cannot handle: say what, and offer a way forward.</summary>
    public class ErrorScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.Error;

        [SerializeField] TMP_Text message;
        [SerializeField] Button retryButton;
        [SerializeField] Button homeButton;

        string _last = "Something went wrong.";

        protected override void OnBind()
        {
            Hook(retryButton, () => Ctx.Flow.BeginScan());
            Hook(homeButton, () => Ctx.Flow.RestartSession());
            GameEvents.OnErrorRaised += OnError;
        }

        void OnDestroy() => GameEvents.OnErrorRaised -= OnError;

        void OnError(string code, string text)
        {
            if (!string.IsNullOrEmpty(text)) _last = text;
        }

        public override void Refresh() => SetText(message, _last);
    }
}
