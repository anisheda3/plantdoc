using NUnit.Framework;
using PlantDoctor.Core;
using UnityEngine;

namespace PlantDoctor.Tests
{
    public class StateMachineTests
    {
        class ProbeState : StateBase
        {
            readonly SimState _id;
            public int Entered, Exited;
            public ProbeState(SimState id) => _id = id;
            public override SimState Id => _id;
            public override void Enter() => Entered++;
            public override void Exit() => Exited++;
        }

        [Test]
        public void TransitionsCallEnterAndExit()
        {
            var go = new GameObject("fsm");
            var machine = go.AddComponent<SimulationStateManager>();
            var welcome = new ProbeState(SimState.Welcome);
            var scan = new ProbeState(SimState.Scan);
            machine.Register(welcome);
            machine.Register(scan);

            machine.Begin(SimState.Welcome);
            Assert.AreEqual(1, welcome.Entered);

            machine.Transition(SimState.Scan);
            Assert.AreEqual(1, welcome.Exited);
            Assert.AreEqual(1, scan.Entered);
            Assert.AreEqual(SimState.Scan, machine.Current);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void UnregisteredStateDoesNotThrow()
        {
            var go = new GameObject("fsm");
            var machine = go.AddComponent<SimulationStateManager>();
            machine.Begin(SimState.Welcome);
            Assert.DoesNotThrow(() => machine.Transition(SimState.Prevention));
            Object.DestroyImmediate(go);
        }
    }
}
