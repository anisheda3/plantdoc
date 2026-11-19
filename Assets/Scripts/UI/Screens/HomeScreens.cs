using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>Text helpers shared by the pages.</summary>
    public static class UIText
    {
        public static string Relative(DateTime local)
        {
            var now = DateTime.Now;
            string time = local.ToString("h:mm tt");
            if (local.Date == now.Date) return "Today, " + time;
            if (local.Date == now.Date.AddDays(-1)) return "Yesterday, " + time;
            return local.ToString("d MMM yyyy") + ", " + time;
        }

        public static string Percent(float value01) => $"{Mathf.RoundToInt(Mathf.Clamp01(value01) * 100f)}%";

        public const string UserNamePref = "plantdoctor.userName";

        public static string UserName
        {
            get { try { return PlayerPrefs.GetString(UserNamePref, ""); } catch (Exception) { return ""; } }
            set { try { PlayerPrefs.SetString(UserNamePref, value ?? ""); } catch (Exception) { } }
        }
    }
}
