using System.Collections.Generic;

namespace PlantDoctor.Core
{
    public enum ScanIssue { None, InsufficientLighting, ExcessiveBlur, LeafTooSmall, LeafOutOfFrame, ExcessiveMotion, Occlusion, UnsupportedPlant }

    /// <summary>Result of the pre-inference gate. Unusable images are never sent to the classifier.</summary>
    public class ScanQualityReport
    {
        public bool IsAcceptable => Issues.Count == 0;
        public List<ScanIssue> Issues = new List<ScanIssue>();
        public float Brightness, Sharpness, Coverage, Motion;

        public string UserMessage()
        {
            if (IsAcceptable) return "Scan quality good.";
            var first = Issues[0];
            return first switch
            {
                ScanIssue.InsufficientLighting => "Lighting is too low. Move to a brighter spot.",
                ScanIssue.ExcessiveBlur => "Image is blurry. Hold the device steady.",
                ScanIssue.LeafTooSmall => "Move closer so the leaf fills most of the frame.",
                ScanIssue.LeafOutOfFrame => "Keep the whole leaf inside the frame.",
                ScanIssue.ExcessiveMotion => "Too much movement. Hold still and retry.",
                ScanIssue.Occlusion => "The leaf is partly blocked. Clear the view.",
                ScanIssue.UnsupportedPlant => "This plant is not supported yet.",
                _ => "Scan quality low. Please retry."
            };
        }
    }
}
