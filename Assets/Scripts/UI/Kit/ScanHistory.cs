using System;
using System.Collections.Generic;
using System.IO;
using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.UI
{
    [Serializable]
    public class ScanRecord
    {
        public string id;
        public long utcTicks;
        public string plantId;
        public string plantName;
        public string diseaseId;
        public string diseaseName;      // "Healthy" when no disease was found
        public bool isHealthy;
        public float severity;
        public float confidence;
        public string origin;           // demo | server | device
        public string thumbnailFile;    // file name inside the history folder
        public string reportFile;       // set once a report PDF was saved

        public DateTime LocalTime => new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
        public bool IsDemo => origin == "demo";
    }

    [Serializable]
    class ScanRecordList { public List<ScanRecord> records = new List<ScanRecord>(); }

    /// <summary>
    /// The user's past scans, kept on the device (a small JSON file plus a thumbnail per scan).
    /// Nothing leaves the phone. A diagnosis is recorded once, when the diagnosis page opens.
    /// </summary>
    public class ScanHistory : MonoBehaviour
    {
        [SerializeField] AppFlowController flow;
        [SerializeField] int keepLast = 60;

        const string JsonName = "history.json";
        ScanRecordList _data = new ScanRecordList();
        readonly Dictionary<string, Texture2D> _thumbs = new Dictionary<string, Texture2D>();
        PlantAnalysisResult _recorded;

        public IReadOnlyList<ScanRecord> Records => _data.records;
        public event Action Changed;

        /// <summary>Folder for history files; overridden by tests so they never touch real data.</summary>
        public string Folder { get; set; }

        void Awake()
        {
            if (string.IsNullOrEmpty(Folder)) Folder = Path.Combine(Application.persistentDataPath, "PlantDoctor");
            Load();
        }

        void OnEnable() => GameEvents.OnStateChanged += HandleStateChanged;

        void OnDisable() => GameEvents.OnStateChanged -= HandleStateChanged;

        static void Dispose(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void OnDestroy()
        {
            foreach (var t in _thumbs.Values) if (t != null) Destroy(t);
            _thumbs.Clear();
        }

        void HandleStateChanged(SimState from, SimState to)
        {
            if (to != SimState.Diagnosis || flow == null) return;
            var ctx = flow.Context;
            if (ctx?.Analysis == null || ctx.Analysis == _recorded) return;   // once per scan
            _recorded = ctx.Analysis;
            Add(ctx, flow.LastScanImage);
        }

        public ScanRecord Add(SessionContext ctx, Texture scanImage)
        {
            var a = ctx.Analysis;
            var record = new ScanRecord
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 12),
                utcTicks = DateTime.UtcNow.Ticks,
                plantId = ctx.Plant != null ? ctx.Plant.plantId : a.plantId,
                plantName = ctx.Plant != null ? ctx.Plant.plantName : (a.plantName ?? "Plant"),
                diseaseId = ctx.IsHealthy ? "healthy" : (ctx.Disease != null ? ctx.Disease.diseaseId : a.diseaseId),
                diseaseName = ctx.IsHealthy ? "Healthy" : (ctx.Disease != null ? ctx.Disease.diseaseName : (a.diseaseName ?? "Unknown")),
                isHealthy = ctx.IsHealthy,
                severity = a.severity,
                confidence = a.confidence,
                origin = a.origin == AnalysisOrigin.Mock ? "demo" : a.origin == AnalysisOrigin.Remote ? "server" : "device",
            };

            if (scanImage == null && ctx.Disease != null && ctx.Disease.referencePhotos != null && ctx.Disease.referencePhotos.Length > 0)
                scanImage = ctx.Disease.referencePhotos[0];   // demo scans use the licensed reference photo
            if (scanImage != null) record.thumbnailFile = SaveThumbnail(record.id, scanImage);

            _data.records.Insert(0, record);
            while (_data.records.Count > keepLast) RemoveAt(_data.records.Count - 1, false);
            Save();
            Changed?.Invoke();
            return record;
        }

        public void SetReportFile(string id, string fileName)
        {
            var r = _data.records.Find(x => x.id == id);
            if (r == null) return;
            r.reportFile = fileName;
            Save();
            Changed?.Invoke();
        }

        public void Delete(string id)
        {
            int i = _data.records.FindIndex(x => x.id == id);
            if (i < 0) return;
            RemoveAt(i, true);
            Changed?.Invoke();
        }

        void RemoveAt(int index, bool save)
        {
            var r = _data.records[index];
            _data.records.RemoveAt(index);
            if (!string.IsNullOrEmpty(r.thumbnailFile))
            {
                if (_thumbs.TryGetValue(r.id, out var t)) { if (t != null) Destroy(t); _thumbs.Remove(r.id); }
                TryDelete(Path.Combine(Folder, r.thumbnailFile));
            }
            if (save) Save();
        }

        /// <summary>The saved thumbnail of a scan, loaded once and cached. Null if it was never saved or is gone.</summary>
        public Texture2D Thumbnail(ScanRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.thumbnailFile)) return null;
            if (_thumbs.TryGetValue(record.id, out var cached) && cached != null) return cached;
            try
            {
                string path = Path.Combine(Folder, record.thumbnailFile);
                if (!File.Exists(path)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!tex.LoadImage(File.ReadAllBytes(path))) { Dispose(tex); return null; }
                _thumbs[record.id] = tex;
                return tex;
            }
            catch (Exception e)
            {
                Log.Warn(Log.Channel.UI, "Could not load a scan thumbnail: " + e.Message);
                return null;
            }
        }

        string SaveThumbnail(string id, Texture source)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var copy = ReadableCopy(source, 420);
                string file = id + ".jpg";
                File.WriteAllBytes(Path.Combine(Folder, file), copy.EncodeToJPG(86));
                Dispose(copy);
                return file;
            }
            catch (Exception e)
            {
                Log.Warn(Log.Channel.UI, "Could not save the scan thumbnail: " + e.Message);
                return null;
            }
        }

        /// <summary>Copies any texture (even unreadable or compressed) into a small readable one.</summary>
        public static Texture2D ReadableCopy(Texture source, int maxSide)
        {
            float scale = Mathf.Min(1f, maxSide / (float)Mathf.Max(source.width, source.height));
            int w = Mathf.Max(2, Mathf.RoundToInt(source.width * scale)), h = Mathf.Max(2, Mathf.RoundToInt(source.height * scale));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGB24, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        void Load()
        {
            try
            {
                string path = Path.Combine(Folder, JsonName);
                if (!File.Exists(path)) return;
                var loaded = JsonUtility.FromJson<ScanRecordList>(File.ReadAllText(path));
                if (loaded?.records != null) _data = loaded;
            }
            catch (Exception e)
            {
                Log.Warn(Log.Channel.UI, "History file unreadable, starting empty: " + e.Message);
                _data = new ScanRecordList();
            }
        }

        void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, JsonName);
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(_data));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Log.Warn(Log.Channel.UI, "Could not save history: " + e.Message);
            }
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { /* best effort */ }
        }
    }
}
