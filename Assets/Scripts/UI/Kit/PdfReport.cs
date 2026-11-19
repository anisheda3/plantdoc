using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.UI
{
    /// <summary>Everything the report shows, as plain data (so the PDF and the screen cannot disagree).</summary>
    public class ReportData
    {
        public string PlantName = "Plant";
        public string ScientificName = "";
        public string Condition = "";
        public string SeverityLabel = "";
        public string ConfidenceText = "";           // empty for demo results: no invented confidence
        public string SourceText = "";
        public bool IsDemo;
        public DateTime ScanTime = DateTime.Now;
        public string Summary = "";
        public readonly List<string> Symptoms = new List<string>();
        public readonly List<(string text, bool done)> Treatment = new List<(string, bool)>();
        public readonly List<(string text, bool done)> Prevention = new List<(string, bool)>();
        public readonly List<string> Actions = new List<string>();
        public readonly List<string> Notes = new List<string>();
        public byte[] PhotoJpeg;
        public int PhotoWidth, PhotoHeight;

        public string SuggestedFileName =>
            $"PlantDoctor_{Sanitize(PlantName)}_{ScanTime:yyyy-MM-dd_HHmm}.pdf";

        static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "") sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            return sb.Length == 0 ? "plant" : sb.ToString();
        }
    }

    /// <summary>
    /// A minimal PDF writer (A4, Helvetica, one embedded JPEG) so the report can be saved as a real
    /// file without any plugin. Text is wrapped using Helvetica's character widths, pages are added
    /// as needed, and non-Latin characters are replaced by safe ASCII.
    /// </summary>
    public static class PdfReport
    {
        const float W = 595f, H = 842f, Margin = 48f;

        // Helvetica advance widths for ASCII 32..126, in 1/1000 em.
        static readonly int[] Widths =
        {
            278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
            1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
            333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,556,556,333,500,278,556,500,722,500,500,500,334,260,334,584
        };

        static float TextWidth(string s, float size, bool bold)
        {
            float w = 0f;
            foreach (char c in s) w += (c >= 32 && c <= 126 ? Widths[c - 32] : 556) * (bold ? 1.06f : 1f);
            return w * size / 1000f;
        }

        public static string Ascii(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '‘': case '’': sb.Append('\''); break;
                    case '“': case '”': sb.Append('"'); break;
                    case '–': case '—': sb.Append('-'); break;
                    case '•': case '·': sb.Append('-'); break;
                    case '…': sb.Append("..."); break;
                    case '×': sb.Append('x'); break;
                    case '\n': sb.Append('\n'); break;
                    default: sb.Append(c >= 32 && c <= 126 ? c : (c == '\t' ? ' ' : '?')); break;
                }
            }
            return sb.ToString();
        }

        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        static List<string> Wrap(string text, float size, bool bold, float width)
        {
            var lines = new List<string>();
            foreach (var paragraph in Ascii(text).Split('\n'))
            {
                var line = new StringBuilder();
                foreach (var word in paragraph.Split(' '))
                {
                    string trial = line.Length == 0 ? word : line + " " + word;
                    if (TextWidth(trial, size, bold) > width && line.Length > 0) { lines.Add(line.ToString()); line.Clear(); line.Append(word); }
                    else { line.Clear(); line.Append(trial); }
                }
                lines.Add(line.ToString());
            }
            return lines;
        }

        sealed class Page
        {
            public readonly StringBuilder Content = new StringBuilder();
            public bool UsesImage;
        }

        sealed class Writer
        {
            public readonly List<Page> Pages = new List<Page>();
            public Page Current;
            public float Y;

            public Writer() { NewPage(); }

            public void NewPage() { Current = new Page(); Pages.Add(Current); Y = H - Margin; }

            public void Need(float height) { if (Y - height < Margin) NewPage(); }

            public void Text(float x, string text, float size, bool bold, float r, float g, float b)
            {
                Current.Content.Append($"BT /{(bold ? "F2" : "F1")} {F(size)} Tf {F(r)} {F(g)} {F(b)} rg 1 0 0 1 {F(x)} {F(Y)} Tm ({Escape(text)}) Tj ET\n");
            }

            public void Paragraph(string text, float size, bool bold, float indent = 0f, float[] color = null, float lead = 1.32f)
            {
                color ??= new[] { 0.125f, 0.2f, 0.157f };
                foreach (var line in Wrap(text, size, bold, W - 2 * Margin - indent))
                {
                    Need(size * lead);
                    Y -= size * lead;
                    Text(Margin + indent, line, size, bold, color[0], color[1], color[2]);
                }
            }

            public void Gap(float h) { Y -= h; }

            public void FilledRect(float x, float y, float w, float h, float r, float g, float b) =>
                Current.Content.Append($"{F(r)} {F(g)} {F(b)} rg {F(x)} {F(y)} {F(w)} {F(h)} re f\n");
        }

        static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        public static byte[] Build(ReportData d)
        {
            var w = new Writer();
            var forest = new[] { 0.09f, 0.247f, 0.165f };
            var grey = new[] { 0.41f, 0.467f, 0.42f };

            // Header band
            w.FilledRect(0, H - 92, W, 92, forest[0], forest[1], forest[2]);
            w.Y = H - 44;
            w.Text(Margin, "Plant Doctor", 24, true, 1, 1, 1);
            w.Y = H - 66;
            w.Text(Margin, "Plant Health Report", 13, false, 0.85f, 0.92f, 0.86f);
            w.Y = H - 92 - 28;

            // Photo + facts
            float photoSize = 130f;
            float top = w.Y;
            if (d.PhotoJpeg != null && d.PhotoWidth > 0)
            {
                float scale = Mathf.Min(photoSize / d.PhotoWidth, photoSize / d.PhotoHeight);
                float pw = d.PhotoWidth * scale, ph = d.PhotoHeight * scale;
                w.Current.Content.Append($"q {F(pw)} 0 0 {F(ph)} {F(Margin)} {F(top - ph)} cm /Im1 Do Q\n");
                w.Current.UsesImage = true;
            }
            float tx = Margin + photoSize + 22f;
            float ty = top;
            void Fact(string label, string value, bool big = false)
            {
                if (string.IsNullOrEmpty(value)) return;
                ty -= 12; w.Y = ty; w.Text(tx, Ascii(label).ToUpperInvariant(), 8, false, grey[0], grey[1], grey[2]);
                ty -= big ? 19 : 15; w.Y = ty; w.Text(tx, Ascii(value), big ? 17 : 12, big, 0.125f, 0.2f, 0.157f);
            }
            Fact("Plant", d.PlantName, true);
            if (!string.IsNullOrEmpty(d.ScientificName)) { ty -= 14; w.Y = ty; w.Text(tx, "(" + Ascii(d.ScientificName) + ")", 10, false, grey[0], grey[1], grey[2]); }
            Fact("Condition", d.Condition, true);
            Fact("Severity", d.SeverityLabel);
            Fact("Confidence", d.ConfidenceText);
            Fact("Source", d.SourceText);
            Fact("Scanned", d.ScanTime.ToString("d MMM yyyy, h:mm tt", System.Globalization.CultureInfo.InvariantCulture));
            w.Y = Mathf.Min(ty, top - photoSize) - 18f;

            void Heading(string title) { w.Gap(8); w.Need(40); w.Y -= 16; w.Text(Margin, Ascii(title), 13, true, forest[0], forest[1], forest[2]); w.Y -= 4; }
            void Bullets(IEnumerable<string> items)
            {
                foreach (var item in items) { w.Paragraph("-  " + item, 10.5f, false, 6f); }
            }

            Heading("Summary");
            w.Paragraph(d.Summary, 10.5f, false);

            if (d.Symptoms.Count > 0) { Heading("Observed symptoms"); Bullets(d.Symptoms); }

            if (d.Treatment.Count > 0)
            {
                Heading("Recommended treatment");
                int n = 1;
                foreach (var (text, done) in d.Treatment) w.Paragraph($"{n++}.  {text}   [{(done ? "done in the app" : "not applied")}]", 10.5f, false, 6f);
            }

            if (d.Prevention.Count > 0)
            {
                Heading("Prevention checklist");
                foreach (var (text, done) in d.Prevention) w.Paragraph($"{(done ? "[x]" : "[ ]")}  {text}", 10.5f, false, 6f);
            }

            if (d.Actions.Count > 0) { Heading("Actions taken in the app"); Bullets(d.Actions); }

            if (d.Notes.Count > 0)
            {
                Heading("Please note");
                foreach (var note in d.Notes) w.Paragraph(note, 9, false, 0f, grey);
            }

            return Assemble(w, d);
        }

        static byte[] Assemble(Writer w, ReportData d)
        {
            // Objects: 1 catalog, 2 pages, 3 F1, 4 F2, 5 image (optional), then page+content pairs.
            var objects = new List<byte[]>();
            void Add(string s) => objects.Add(Encoding.ASCII.GetBytes(s));

            bool hasImage = d.PhotoJpeg != null && d.PhotoWidth > 0;
            int firstPage = hasImage ? 6 : 5;     // object number of the first page
            var kids = new StringBuilder();
            for (int i = 0; i < w.Pages.Count; i++) kids.Append($"{firstPage + i * 2} 0 R ");

            Add("<< /Type /Catalog /Pages 2 0 R >>");
            Add($"<< /Type /Pages /Kids [{kids}] /Count {w.Pages.Count} >>");
            Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
            if (hasImage)
            {
                var head = Encoding.ASCII.GetBytes($"<< /Type /XObject /Subtype /Image /Width {d.PhotoWidth} /Height {d.PhotoHeight} " +
                                                   $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {d.PhotoJpeg.Length} >>\nstream\n");
                var tail = Encoding.ASCII.GetBytes("\nendstream");
                var obj = new byte[head.Length + d.PhotoJpeg.Length + tail.Length];
                Buffer.BlockCopy(head, 0, obj, 0, head.Length);
                Buffer.BlockCopy(d.PhotoJpeg, 0, obj, head.Length, d.PhotoJpeg.Length);
                Buffer.BlockCopy(tail, 0, obj, head.Length + d.PhotoJpeg.Length, tail.Length);
                objects.Add(obj);
            }
            for (int i = 0; i < w.Pages.Count; i++)
            {
                int content = firstPage + i * 2 + 1;
                string xobj = hasImage && w.Pages[i].UsesImage ? " /XObject << /Im1 5 0 R >>" : "";
                Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(W)} {F(H)}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >>{xobj} >> /Contents {content} 0 R >>");
                string stream = w.Pages[i].Content.ToString();
                Add($"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream");
            }

            using (var ms = new MemoryStream())
            {
                void Write(byte[] b) => ms.Write(b, 0, b.Length);
                Write(Encoding.ASCII.GetBytes("%PDF-1.4\n"));
                var offsets = new List<long>();
                for (int i = 0; i < objects.Count; i++)
                {
                    offsets.Add(ms.Position);
                    Write(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
                    Write(objects[i]);
                    Write(Encoding.ASCII.GetBytes("\nendobj\n"));
                }
                long xref = ms.Position;
                var sb = new StringBuilder($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
                foreach (var o in offsets) sb.Append(o.ToString("0000000000")).Append(" 00000 n \n");
                sb.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
                Write(Encoding.ASCII.GetBytes(sb.ToString()));
                return ms.ToArray();
            }
        }
    }

    /// <summary>Saves a finished report: always into the app's own folder, and, on Android, a copy in Downloads.</summary>
    public static class ReportExporter
    {
        public struct Result
        {
            public bool Success;
            public string Message;      // what to tell the user, honestly
            public string AppPath;
        }

        public static string ReportsFolder => Path.Combine(Application.persistentDataPath, "PlantDoctor", "Reports");

        public static Result Save(byte[] pdf, string fileName)
        {
            var result = new Result();
            try
            {
                Directory.CreateDirectory(ReportsFolder);
                result.AppPath = Path.Combine(ReportsFolder, fileName);
                File.WriteAllBytes(result.AppPath, pdf);
                result.Success = true;
                result.Message = "Saved in the app: " + fileName;
            }
            catch (Exception e)
            {
                Log.Warn(Log.Channel.UI, "Report save failed: " + e);
                return new Result { Success = false, Message = "The report could not be saved: " + e.Message };
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                SaveToDownloads(pdf, fileName);
                result.Message = "Saved to Downloads/Plant Doctor: " + fileName;
            }
            catch (Exception e)
            {
                // Older Android versions have no MediaStore downloads; the app copy still exists.
                Log.Warn(Log.Channel.UI, "Copy to Downloads failed: " + e.Message);
                result.Message = "Saved inside the app (a copy in Downloads was not possible on this phone): " + fileName;
            }
#endif
            return result;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void SaveToDownloads(byte[] bytes, string fileName)
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (var values = new AndroidJavaObject("android.content.ContentValues"))
            {
                values.Call("put", "_display_name", fileName);
                values.Call("put", "mime_type", "application/pdf");
                values.Call("put", "relative_path", "Download/Plant Doctor");
                using (var downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads"))
                using (var collection = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
                using (var uri = resolver.Call<AndroidJavaObject>("insert", collection, values))
                {
                    if (uri == null) throw new Exception("MediaStore refused the file");
                    using (var stream = resolver.Call<AndroidJavaObject>("openOutputStream", uri))
                    {
                        stream.Call("write", bytes, 0, bytes.Length);
                        stream.Call("close");
                    }
                }
            }
        }
#endif
    }
}
