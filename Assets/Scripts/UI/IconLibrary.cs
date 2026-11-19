using System;
using TMPro;
using UnityEngine;

namespace PlantDoctor.UI
{
    /// <summary>Line icons by name (Lucide), so runtime code can pick an icon from data.</summary>
    [CreateAssetMenu(menuName = "PlantDoctor/Icon Library", fileName = "IconLibrary")]
    public class IconLibrary : ScriptableObject
    {
        [SerializeField] string[] names = new string[0];
        [SerializeField] Sprite[] sprites = new Sprite[0];

        public void Set(string[] iconNames, Sprite[] iconSprites)
        {
            names = iconNames; sprites = iconSprites;
        }

        public Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < names.Length; i++)
                if (names[i] == name) return sprites[i];
            return null;
        }
    }
}
