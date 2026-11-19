using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Diseases;
using UnityEngine;

namespace PlantDoctor.Utilities
{
    /// <summary>
    /// Phase 9 polish: centralises audio cues for milestone events so no gameplay
    /// system needs to know about sound. Clips are optional; missing clips are skipped.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FeedbackDirector : MonoBehaviour
    {
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip scanSuccess;
        [SerializeField] AudioClip scanRejected;
        [SerializeField] AudioClip diagnosisRevealed;
        [SerializeField] AudioClip treatmentStepDone;
        [SerializeField] AudioClip recoveryComplete;
        [SerializeField] AudioClip errorCue;
        [SerializeField] [Range(0f, 1f)] float volume = 0.8f;

        void Awake() => audioSource ??= GetComponent<AudioSource>();

        void OnEnable()
        {
            GameEvents.OnScanCompleted += HandleScanCompleted;
            GameEvents.OnScanQualityFailed += HandleScanRejected;
            GameEvents.OnDiseaseDetected += HandleDiagnosis;
            GameEvents.OnTreatmentStepCompleted += HandleStepCompleted;
            GameEvents.OnRecoveryCompleted += HandleRecoveryCompleted;
            GameEvents.OnErrorRaised += HandleError;
        }

        void OnDisable()
        {
            GameEvents.OnScanCompleted -= HandleScanCompleted;
            GameEvents.OnScanQualityFailed -= HandleScanRejected;
            GameEvents.OnDiseaseDetected -= HandleDiagnosis;
            GameEvents.OnTreatmentStepCompleted -= HandleStepCompleted;
            GameEvents.OnRecoveryCompleted -= HandleRecoveryCompleted;
            GameEvents.OnErrorRaised -= HandleError;
        }

        void HandleScanCompleted() => Play(scanSuccess);
        void HandleScanRejected(ScanQualityReport r) => Play(scanRejected);
        void HandleDiagnosis(DiseaseData d, ConfidenceBand b) => Play(diagnosisRevealed);
        void HandleStepCompleted(Treatment.TreatmentStep s, int i, int t) => Play(treatmentStepDone);
        void HandleRecoveryCompleted() => Play(recoveryComplete);
        void HandleError(string code, string message) => Play(errorCue);

        void Play(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
