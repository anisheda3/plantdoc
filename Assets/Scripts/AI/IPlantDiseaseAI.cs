using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PlantDoctor.AI
{
    /// <summary>Inference abstraction. Remote, local and mock backends all implement this.</summary>
    public interface IPlantDiseaseAI
    {
        string DisplayName { get; }
        AnalysisOrigin Origin { get; }
        Task<PlantAnalysisResult> AnalyzePlant(Texture2D image, CancellationToken token = default);
    }
}
