using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// Makes a RawImage fill its rect with the texture's centre ("cover"), cropping instead of
    /// stretching. Re-fits whenever the rect changes shape.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class CoverImage : MonoBehaviour
    {
        [SerializeField] Vector2 focus = new Vector2(0.5f, 0.5f);
        RawImage _image;

        RawImage Image => _image != null ? _image : (_image = GetComponent<RawImage>());

        public void Set(Texture texture)
        {
            Image.texture = texture;
            Image.color = texture != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            Fit();
        }

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();

        void Fit()
        {
            var img = Image;
            var tex = img.texture;
            var rt = (RectTransform)transform;
            if (tex == null || rt.rect.width < 1f || rt.rect.height < 1f) { img.uvRect = new Rect(0, 0, 1, 1); return; }
            float rectAspect = rt.rect.width / rt.rect.height;
            float texAspect = tex.width / (float)tex.height;
            float w = 1f, h = 1f;
            if (texAspect > rectAspect) w = rectAspect / texAspect; else h = texAspect / rectAspect;
            float x = Mathf.Clamp01(focus.x - w * 0.5f) * 1f;
            float y = Mathf.Clamp01(focus.y - h * 0.5f) * 1f;
            x = Mathf.Min(x, 1f - w); y = Mathf.Min(y, 1f - h);
            img.uvRect = new Rect(x, y, w, h);
        }
    }
}
