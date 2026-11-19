using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// The first page: the plant in its backdrop and one thing to do, scan a plant. A small gear
    /// opens the profile (settings and past scans), which keeps the home page uncluttered.
    /// </summary>
    public class HomeScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.Home;

        [SerializeField] TMP_Text greeting;
        [SerializeField] Button scanButton;
        [SerializeField] Button profileButton;

        protected override void OnBind()
        {
            Hook(scanButton, () => Ctx.Flow?.BeginScan());
            Hook(profileButton, () => Ctx.Router.ShowProfile());
        }

        public override void Refresh()
        {
            string name = UIText.UserName;
            SetText(greeting, string.IsNullOrWhiteSpace(name) ? "Keep your plants healthy" : $"Hello, {name.Trim()}");
        }
    }
}
