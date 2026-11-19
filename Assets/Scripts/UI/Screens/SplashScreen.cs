using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>The opening screen: logo, tagline, quiet loading indicator. The router moves on by itself.</summary>
    public class SplashScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.Splash;
    }
}
