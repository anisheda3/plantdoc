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
    /// <summary>A line with a status icon (done / not done) and text.</summary>
    public class CheckRowView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text text;

        public void Bind(UITheme theme, string value, bool done)
        {
            if (text != null) text.text = value;
            if (icon != null)
            {
                icon.sprite = theme.Icon(done ? "circle-check" : "circle");
                icon.color = done ? theme.botanical : theme.textTertiary;
            }
        }
    }
}
