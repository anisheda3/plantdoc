// ---------------------------------------------------------------------------
// Minimal UnityEngine API surface for HEADLESS verification only.
//
// This file is deliberately OUTSIDE Assets/ so the Unity Editor never compiles it.
// It exists so the PlantDoctor runtime scripts can be type-checked and unit-tested
// on a build server (or in a container) that has no Unity installation.
//
// It is NOT a Unity emulator: methods are stubs. It validates that the code
// compiles and that pure logic (parsing, state maths, data resolution) is correct.
// Behaviour that depends on the real engine must still be tested in the Editor.
//
// Usage:  cd tools/headless-verify && dotnet test
// ---------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color yellow => new Color(1, 1, 0);
        public static Color white => new Color(1, 1, 1);
        public static Color red => new Color(1, 0, 0);
        public static Color green => new Color(0, 1, 0);
        public static Color gray => new Color(.5f, .5f, .5f);
        public static Color clear => new Color(0, 0, 0, 0);
        public static Color Lerp(Color a, Color b, float t) => a;
    }
    public struct Color32 { public byte r, g, b, a; }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.x * b, a.y * b);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
    }
    public struct Vector2Int { public Vector2Int(int x, int y) { } }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z = 0) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public float magnitude => 0f;
        public static Vector3 operator *(Vector3 a, float b) => a;
        public static Vector3 operator +(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static float Distance(Vector3 a, Vector3 b) => 0f;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
    }
    public struct Quaternion
    {
        public static Quaternion identity => new Quaternion();
        public static Quaternion LookRotation(Vector3 f) => identity;
        public static Quaternion Euler(float x, float y, float z) => identity;
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public float xMin => x; public float yMin => y;
        public float xMax => x + width; public float yMax => y + height;
        public Vector2 center => new Vector2(x + width / 2, y + height / 2);
        public Vector2 position => new Vector2(x, y);
        public Vector2 size => new Vector2(width, height);
        public static bool operator ==(Rect a, Rect b) =>
            a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;
        public static bool operator !=(Rect a, Rect b) => !(a == b);
        public override bool Equals(object o) => o is Rect r && this == r;
        public override int GetHashCode() => (int)(x + y + width + height);
    }
    public struct RectInt { public RectInt(int x, int y, int w, int h) { } }
    public struct Pose
    {
        public Vector3 position; public Quaternion rotation;
        public Pose(Vector3 p, Quaternion r) { position = p; rotation = r; }
    }
    public static class Mathf
    {
        public static float LerpAngle(float a,float b,float t)=>a+(b-a)*t;
        public static float SmoothStep(float a,float b,float t)=>a+(b-a)*t;
        public const float Epsilon = 1e-5f;
        public const float PI = 3.14159265f;
        public const float Deg2Rad = 0.0174533f;
        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Abs(float v) => Math.Abs(v);
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float PingPong(float t, float l) => 0f;
        public static float InverseLerp(float a, float b, float v) => 0f;
        public static float MoveTowards(float a, float b, float d) => b;
    }
    public class Object
    {
        public string name;
        public static void DestroyImmediate(Object o) { }
        public static void Destroy(Object o) { }
        public static T Instantiate<T>(T original) where T : Object => original;
        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;
        public static T FindObjectOfType<T>() where T : Object => null;
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
    }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }
    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Transform parent { get; set; }
        public int childCount => 0;
        public Transform GetChild(int i) => null;
        public void SetParent(Transform p, bool w = true) { }
        public void LookAt(Transform t) { }
        public void LookAt(Vector3 v) { }
        public IEnumerator GetEnumerator() => null;
        public Vector3 forward => Vector3.zero;
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { }
    }
    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        public T[] GetComponentsInChildren<T>() => Array.Empty<T>();
        public T GetComponentInParent<T>() => default;
    }
    public class Behaviour : Component { public bool enabled { get; set; } public bool isActiveAndEnabled => true; }
    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator r) => null;
        public void StopCoroutine(Coroutine c) { }
        public void StopAllCoroutines() { }
    }
    public class Coroutine { }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public class GameObject : Object
    {
        public static GameObject Find(string name) => null;
        readonly List<Component> _c = new List<Component>();
        public GameObject() { }
        public GameObject(string n) { name = n; }
        public Transform transform => null;
        public bool activeSelf => true;
        public void SetActive(bool v) { }
        public T AddComponent<T>() where T : Component, new() { var c = new T(); _c.Add(c); return c; }
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        public static GameObject CreatePrimitive(PrimitiveType t) => new GameObject();
    }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public class Texture : Object { public int width, height; public FilterMode filterMode { get; set; } }
    public enum TextureFormat { RGB24, RGBA32, ARGB32 }
    public class Texture2D : Texture
    {
        public Texture2D(int w, int h) { width = w; height = h; }
        public Texture2D(int w, int h, TextureFormat f, bool m) { width = w; height = h; }
        public Color GetPixelBilinear(float u, float v) => default;
        public Color GetPixel(int x, int y) => default;
        public void SetPixels(Color[] c) { }
        public void SetPixels32(Color32[] c) { }
        public void SetPixel(int x, int y, Color c) { }
        public void Apply(bool b = false) { }
        public byte[] EncodeToJPG(int q) => Array.Empty<byte>();
        public byte[] EncodeToPNG() => Array.Empty<byte>();
        public Color[] GetPixels() => Array.Empty<Color>();
        public bool LoadImage(byte[] d) => true;
        public void ReadPixels(Rect r, int x, int y) { }
        public void LoadRawTextureData(object d) { }
        public Unity.Collections.NativeArray<T> GetRawTextureData<T>() where T : struct => default;
        public static Texture2D whiteTexture => new Texture2D(1, 1);
        public static Texture2D blackTexture => new Texture2D(1, 1);
    }
    public class RenderTexture : Texture
    {
        public RenderTexture(int w, int h, int d) { width = w; height = h; }
        public static RenderTexture active { get; set; }
        public void Release() { }
    }
    public class Sprite : Object { }
    public class Mesh : Object { public int blendShapeCount; }
    public class Material : Object
    {
        public Material(Shader s) { }
        public void SetFloat(string n, float v) { }
        public void SetColor(string n, Color c) { }
        public void SetTexture(string n, Texture t) { }
        public Color color { get; set; }
    }
    public class Shader : Object
    {
        public static Shader Find(string n) => new Shader();
        public static int PropertyToID(string n) => n.GetHashCode();
    }
    public class Renderer : Component
    {
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
        public bool enabled { get; set; }
        public void GetPropertyBlock(MaterialPropertyBlock b) { }
        public void SetPropertyBlock(MaterialPropertyBlock b) { }
    }
    public class MeshRenderer : Renderer { }
    public class LineRenderer : Renderer
    {
        public int positionCount { get; set; }
        public float startWidth { get; set; }
        public float endWidth { get; set; }
        public void SetPosition(int i, Vector3 p) { }
        public bool useWorldSpace { get; set; }
    }
    public class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh;
        public void SetBlendShapeWeight(int i, float w) { }
    }
    public class MaterialPropertyBlock
    {
        public void SetFloat(int id, float v) { }
        public void SetColor(int id, Color c) { }
    }
    public class Collider : Component { public bool enabled { get; set; } }
    public class BoxCollider : Collider { public Vector3 size { get; set; } public Vector3 center { get; set; } }
    public class Rigidbody : Component { public bool isKinematic { get; set; } public bool useGravity { get; set; } }
    public class ParticleSystem : Component
    {
        public EmissionModule emission => new EmissionModule();
        public struct EmissionModule { public float rateOverTime { get; set; } }
        public void Play() { } public void Stop() { }
    }
    public class AudioClip : Object { public float length => 1f; }
    public class AudioSource : Component
    {
        public AudioClip clip { get; set; }
        public float volume { get; set; }
        public void Play() { } public void Stop() { }
        public void PlayOneShot(AudioClip c, float v = 1f) { }
    }
    public struct Ray { public Vector3 origin; public Vector3 direction; }
    public struct RaycastHit { public Vector3 point; public Collider collider; public float distance; }
    public static class Physics
    {
        public static bool Raycast(Ray r, out RaycastHit h, float d = 1000f) { h = default; return false; }
    }
    public class Camera : Component
    {
        public static Camera main => null;
        public float fieldOfView { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public RenderTexture targetTexture { get; set; }
        public void Render() { }
        public Vector3 WorldToViewportPoint(Vector3 v) => Vector3.zero;
        public Vector3 ViewportToWorldPoint(Vector3 v) => Vector3.zero;
        public Ray ScreenPointToRay(Vector3 p) => new Ray();
        public Vector3 WorldToScreenPoint(Vector3 v) => Vector3.zero;
    }
    public class AnimationCurve
    {
        public AnimationCurve() { }
        public static AnimationCurve EaseInOut(float a, float b, float c, float d) => new AnimationCurve();
        public static AnimationCurve Linear(float a, float b, float c, float d) => new AnimationCurve();
        public float Evaluate(float t) => t;
    }
    public static class Time { public static float deltaTime; public static float time; public static float unscaledDeltaTime; }
    public static class Debug
    {
        public static void Log(object o) { }
        public static void LogWarning(object o) { }
        public static void LogError(object o) { }
    }
    public enum RuntimePlatform { Android, IPhonePlayer, WindowsEditor, OSXEditor }
    public enum UserAuthorization { WebCam, Microphone }
    public class AsyncOperation : IEnumerator
    {
        public bool isDone => true;
        public object Current => null;
        public bool MoveNext() => false;
        public void Reset() { }
    }
    public static class Application
    {
        public static string version = "1.0";
        public static bool isEditor => true;
        public static RuntimePlatform platform => RuntimePlatform.Android;
        public static bool HasUserAuthorization(UserAuthorization u) => true;
        public static AsyncOperation RequestUserAuthorization(UserAuthorization u) => new AsyncOperation();
    }
    public class WaitForSeconds : IEnumerator
    {
        public WaitForSeconds(float s) { }
        public object Current => null; public bool MoveNext() => false; public void Reset() { }
    }
    public class WaitForEndOfFrame : IEnumerator
    {
        public object Current => null; public bool MoveNext() => false; public void Reset() { }
    }
    public class WebCamDevice { public string name => "stub"; }
    public class WebCamTexture : Texture
    {
        public WebCamTexture(string n, int w, int h, int f) { width = w; height = h; }
        public bool isPlaying => true;
        public void Play() { } public void Stop() { }
        public static WebCamDevice[] devices => Array.Empty<WebCamDevice>();
        public void GetPixels32(Color32[] c) { }
    }
    public class SerializeField : Attribute { }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class MinAttribute : Attribute { public MinAttribute(float a) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class TextAreaAttribute : Attribute { public TextAreaAttribute() { } public TextAreaAttribute(int a, int b) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class SpaceAttribute : Attribute { public SpaceAttribute() { } }
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    public class CreateAssetMenuAttribute : Attribute { public string menuName; public string fileName; }
    public class WWWForm
    {
        public void AddBinaryData(string a, byte[] b, string c, string d) { }
        public void AddField(string a, string b) { }
    }
    // ---- IMGUI (used by DemoRunner) ----
    public class GUIStyleState { public Color textColor { get; set; } }
    public class GUIStyle
    {
        public GUIStyle() { } public GUIStyle(GUIStyle o) { }
        public int fontSize { get; set; }
        public bool wordWrap { get; set; }
        public bool richText { get; set; }
        public GUIStyleState normal { get; set; } = new GUIStyleState();
    }
    public class GUISkin { public GUIStyle label => new GUIStyle(); public GUIStyle box => new GUIStyle(); }
    public static class GUI
    {
        public static GUISkin skin => new GUISkin();
        public static Color color { get; set; }
        public static bool enabled { get; set; }
    }
    public class GUILayoutOption { }
    public static class GUILayout
    {
        public static void BeginArea(Rect r, GUIStyle s = null) { }
        public static void EndArea() { }
        public static void Label(string t, params object[] o) { }
        public static void Label(string t, GUIStyle s, params object[] o) { }
        public static bool Button(string t, params object[] o) => false;
        public static void Space(float p) { }
        public static void BeginHorizontal(params object[] o) { }
        public static void EndHorizontal() { }
        public static Vector2 BeginScrollView(Vector2 v, params object[] o) => v;
        public static void EndScrollView() { }
        public static GUILayoutOption Height(float h) => new GUILayoutOption();
        public static GUILayoutOption Width(float w) => new GUILayoutOption();
    }
}
namespace Unity.Collections
{
    public struct NativeArray<T> where T : struct { }
}
namespace UnityEngine.UI
{
    using UnityEngine;
    public class Graphic : Component { public Color color { get; set; } }
    public enum FillMethod { Horizontal, Vertical, Radial90 }
    public class Image : Graphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public Sprite sprite { get; set; }
        public float fillAmount { get; set; }
        public bool enabled { get; set; }
        public Type type { get; set; }
        public FillMethod fillMethod { get; set; }
        public RectTransform rectTransform => null;
    }
    public struct ColorBlock { public Color highlightedColor; public Color pressedColor; }
    public class LayoutElement : Component { public float minHeight { get; set; } public float preferredHeight { get; set; } }
    public class LayoutGroup : Component { public RectOffset padding { get; set; } public float spacing { get; set; }
        public bool childControlHeight { get; set; } public bool childForceExpandHeight { get; set; }
        public bool childControlWidth { get; set; } public bool childForceExpandWidth { get; set; } }
    public class VerticalLayoutGroup : LayoutGroup { }
    public class HorizontalLayoutGroup : LayoutGroup { }
    public class ContentSizeFitter : Component { public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode verticalFit { get; set; } public FitMode horizontalFit { get; set; } }
    public class Mask : Component { }
    public class GraphicRaycaster : Component { }
    public class CanvasScaler : Component { public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize }
        public ScaleMode uiScaleMode { get; set; } public Vector2 referenceResolution { get; set; }
        public float matchWidthOrHeight { get; set; } }
    public class ScrollRect : Component { public bool horizontal { get; set; } public float scrollSensitivity { get; set; }
        public RectTransform content { get; set; } public RectTransform viewport { get; set; } }
    public class RawImage : Graphic { public Texture texture { get; set; } }
    public class Text : Graphic { public string text { get; set; } }
    public class Button : Component
    {
        public ButtonClickedEvent onClick => new ButtonClickedEvent();
        public bool interactable { get; set; }
        public ColorBlock colors { get; set; }
        public class ButtonClickedEvent
        {
            public void AddListener(Action a) { }
            public void RemoveListener(Action a) { }
            public void RemoveAllListeners() { }
            public void Invoke() { }
        }
    }
    public class Slider : Component { public float value { get; set; } }
}
namespace UnityEngine.Networking
{
    using UnityEngine;
    public class UnityWebRequestAsyncOperation { public bool isDone => true; }
    public class DownloadHandler { public string text => "{}"; public byte[] data => Array.Empty<byte>(); }
    public class UnityWebRequest : IDisposable
    {
        public void SetRequestHeader(string name, string value) { }
        public enum Result { Success, ConnectionError, ProtocolError, DataProcessingError }
        public Result result => Result.Success;
        public int timeout; public long responseCode; public string error;
        public DownloadHandler downloadHandler => new DownloadHandler();
        public static UnityWebRequest Post(string url, WWWForm form) => new UnityWebRequest();
        public static UnityWebRequest Get(string url) => new UnityWebRequest();
        public UnityWebRequestAsyncOperation SendWebRequest() => new UnityWebRequestAsyncOperation();
        public void Abort() { } public void Dispose() { }
    }
}

// ---- additions for the Canvas UI layer ----
namespace UnityEngine
{
    public static class ColorUtility { public static string ToHtmlStringRGB(Color c) => "FFFFFF"; }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 anchoredPosition { get; set; }
    }
    public class RectOffset { public RectOffset(int l, int r, int t, int b) { } }
    public class Canvas : Component { public RenderMode renderMode { get; set; } }
}
namespace UnityEngine.EventSystems
{
    public class EventSystem : UnityEngine.Component { }
    public class StandaloneInputModule : UnityEngine.Component { }
}
namespace TMPro
{
    using UnityEngine;
    public enum TextAlignmentOptions { TopLeft, Center, MidlineLeft, MidlineRight, Top }
    public enum FontStyles { Normal, Bold, Italic }
    public class TMP_Text : UnityEngine.Component
    {
        public string text { get; set; }
        public float fontSize { get; set; }
        public Color color { get; set; }
        public TextAlignmentOptions alignment { get; set; }
        public FontStyles fontStyle { get; set; }
        public bool enableWordWrapping { get; set; }
        public bool raycastTarget { get; set; }
        public bool richText { get; set; }
        public RectTransform rectTransform => null;
    }
    public class TextMeshProUGUI : TMP_Text { }
}
// ---- additions for SafeAreaFitter ----
namespace UnityEngine
{
    public enum ScreenOrientation { Portrait, PortraitUpsideDown, LandscapeLeft, LandscapeRight, AutoRotation }
    public static class Screen
    {
        public static int width => 1080;
        public static int height => 1920;
        public static Rect safeArea => new Rect(0, 0, 1080, 1920);
        public static ScreenOrientation orientation => ScreenOrientation.Portrait;
        public static int sleepTimeout { get; set; }
    }
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class DisallowMultipleComponentAttribute : System.Attribute { }
}

// ---- additions for studio lighting (Editor-only code path) ----
namespace UnityEngine
{
    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }
    public enum CameraClearFlags { Skybox, Color, SolidColor, Depth, Nothing }
    public enum FogMode { Linear, Exponential, ExponentialSquared }
    public class Light : Component
    {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public LightShadows shadows { get; set; }
    }
    public class Material2Stub { }
    public static class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode { get; set; }
        public static Color ambientSkyColor { get; set; }
        public static Color ambientEquatorColor { get; set; }
        public static Color ambientGroundColor { get; set; }
        public static Material skybox { get; set; }
        public static bool fog { get; set; }
        public static FogMode fogMode { get; set; }
        public static Color fogColor { get; set; }
        public static float fogStartDistance { get; set; }
        public static float fogEndDistance { get; set; }
    }
}
namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox, Trilight, Flat, Custom }
}

// ---- LogAssert: Unity's Test Runner fails a test on unexpected Debug.LogError.
// The harness has no such rule, so these are no-ops that only need to compile.
namespace UnityEngine { public enum LogType { Error, Assert, Warning, Log, Exception } }
namespace UnityEngine.TestTools
{
    public static class LogAssert
    {
        public static bool ignoreFailingMessages { get; set; }
        public static void Expect(UnityEngine.LogType type, string message) { }
        public static void Expect(UnityEngine.LogType type, System.Text.RegularExpressions.Regex regex) { }
        public static void NoUnexpectedReceived() { }
    }
}
