using PlantDoctor.Core;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.Treatment
{
    /// <summary>Serves disease-specific prevention measures; falls back only when none exist.</summary>
    public class PreventionManager : MonoBehaviour
    {
        [SerializeField] PreventionPlan genericFallbackPlan;

        SessionContext _context;
        PreventionPlan _plan;
        int _index;

        public PreventionMeasure Current =>
            _plan != null && _index >= 0 && _index < _plan.MeasureCount ? _plan.measures[_index] : null;

        public PreventionPlan Plan => _plan;
        public int Index => _index;
        public int Count => _plan?.MeasureCount ?? 0;

        public bool Begin(SessionContext context)
        {
            _context = context;
            _plan = context?.Disease?.preventionPlan;

            if (_plan == null || _plan.MeasureCount == 0)
            {
                Log.Warn(Log.Channel.Sim, "No disease-specific prevention plan; using generic guidance.");
                _plan = genericFallbackPlan;
            }
            _index = 0;
            if (_plan != null && _plan.MeasureCount > 0 && _context != null) _context.PreventionMeasuresReviewed = 1;
            return _plan != null && _plan.MeasureCount > 0;
        }

        public bool Next()
        {
            if (_plan == null) return false;
            if (_index + 1 >= _plan.MeasureCount)
            {
                GameEvents.RaisePreventionCompleted();
                return false;
            }
            _index++;
            if (_context != null) _context.PreventionMeasuresReviewed = _index + 1;
            return true;
        }
    }
}
