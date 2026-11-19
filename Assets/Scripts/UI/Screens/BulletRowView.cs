using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>A bullet with a coloured dot (the colour is decoration; the text carries the meaning).</summary>
    public class BulletRowView : MonoBehaviour
    {
        [SerializeField] Image dot;
        [SerializeField] TMP_Text text;

        public void Bind(string value, Color dotColor)
        {
            if (text != null) text.text = value;
            if (dot != null) dot.color = dotColor;
        }
    }
}
