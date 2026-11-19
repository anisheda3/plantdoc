using System.Collections.Generic;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.Core
{
    /// <summary>
    /// Central FSM. Buttons never mutate other systems directly — they request transitions
    /// or raise events which states interpret.
    /// </summary>
    public class SimulationStateManager : MonoBehaviour
    {
        public SessionContext Context { get; } = new SessionContext();
        public SimState Current { get; private set; } = SimState.Welcome;

        readonly Dictionary<SimState, StateBase> _states = new Dictionary<SimState, StateBase>();
        StateBase _active;

        public void Register(StateBase state)
        {
            if (state == null) return;
            state.Bind(this);
            _states[state.Id] = state;
        }

        public void Begin(SimState entry)
        {
            Current = entry;
            _active = Resolve(entry);
            _active?.Enter();
            Log.Info(Log.Channel.Core, $"FSM start -> {entry}");
        }

        public void Transition(SimState next)
        {
            if (next == Current && _active != null) return;
            var from = Current;
            _active?.Exit();
            _active = Resolve(next);
            Current = next;
            Log.Info(Log.Channel.Core, $"FSM {from} -> {next}");
            GameEvents.RaiseStateChanged(from, next);
            _active?.Enter();
        }

        void Update() => _active?.Tick(Time.deltaTime);

        StateBase Resolve(SimState id)
        {
            if (_states.TryGetValue(id, out var s)) return s;
            Log.Warn(Log.Channel.Core, $"No handler registered for state {id}; running as passive stage.");
            return null;
        }

        public bool Has(SimState id) => _states.ContainsKey(id);
    }
}
