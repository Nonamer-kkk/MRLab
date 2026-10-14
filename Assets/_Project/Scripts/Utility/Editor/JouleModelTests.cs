using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MRLab.Joule.Editor
{
    /// <summary>
    /// Small deterministic smoke suite for the teaching model. It deliberately
    /// avoids NUnit so it can be run from a clean Unity project in one click.
    /// </summary>
    public static class JouleModelTests
    {
        [MenuItem("MRLab/Run Joule Model Tests")]
        public static void Run()
        {
            var results = new List<TestResult>();
            RunTest(results, "I2 proportional heating", TestI2ProportionalHeating);
            RunTest(results, "electrical energy balance and cooling", TestEnergyAndCooling);
            RunTest(results, "breaker versus unprotected", TestBreakerAndUnprotected);
            RunTest(results, "thermal stages and reset", TestStagesAndReset);
            RunTest(results, "wire area trend", TestWireAreaTrend);
            RunTest(results, "normal circuit and copper resistivity", TestNormalCircuit);
            RunTest(results, "deterministic replay model", TestDeterminism);
            RunTest(results, "poor-contact hotspot under breaker rating", TestPoorContact);
            RunTest(results, "manager preparation, pause, review isolation", TestManagerLifecycle);
            RunTest(results, "manager incident, isolation, alarm, extinguishing, reset", TestManagerFireWorkflow);
            RunTest(results, "continuous fault location, lock, replay and previous run", TestFaultLocation);
            RunTest(results, "reset cancels active spray and partial extinguishing", TestSprayReset);
            RunTest(results, "continuous wire geometry and rejection of empty space", TestWireGeometry);
            RunTest(results, "input-neutral tool movement, aim, release and reset", TestToolInteraction);
            RunTest(results, "manager breaker event recorded once", TestManagerBreakerEvent);
            RunTest(results, "CSV units and invariant numeric output", TestCsvExport);

            string directory = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(directory);
            string path = Path.GetFullPath(Path.Combine(directory, "JouleModelTests.json"));
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"utc\": \"" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\",");
            json.AppendLine("  \"passed\": " + results.FindAll(r => r.Passed).Count + ",");
            json.AppendLine("  \"failed\": " + results.FindAll(r => !r.Passed).Count + ",");
            json.AppendLine("  \"tests\": [");
            for (int i = 0; i < results.Count; i++)
            {
                TestResult result = results[i];
                json.Append("    { \"name\": \"").Append(Escape(result.Name)).Append("\", \"passed\": ").Append(result.Passed ? "true" : "false").Append(", \"detail\": \"").Append(Escape(result.Detail)).Append("\" }");
                json.AppendLine(i == results.Count - 1 ? string.Empty : ",");
            }

            json.AppendLine("  ]");
            json.AppendLine("}");
            File.WriteAllText(path, json.ToString(), Encoding.UTF8);

            int failed = results.FindAll(r => !r.Passed).Count;
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "Joule model tests: {0} passed, {1} failed. Evidence: {2}", results.Count - failed, failed, path));
            if (failed > 0)
            {
                throw new InvalidOperationException("One or more Joule model tests failed. See " + path);
            }
        }

        private static void RunTest(List<TestResult> results, string name, Action test)
        {
            try
            {
                test();
                results.Add(new TestResult(name, true, "ok"));
            }
            catch (Exception exception)
            {
                results.Add(new TestResult(name, false, exception.Message));
                Debug.LogError(name + ": " + exception.Message);
            }
        }

        private static void TestI2ProportionalHeating()
        {
            var low = new JouleModel();
            var high = new JouleModel();
            low.Reset(new JouleParameters { Voltage = 12f, ProtectionEnabled = false });
            high.Reset(new JouleParameters { Voltage = 24f, ProtectionEnabled = false });
            low.InjectFault(FaultMode.ShortCircuit);
            high.InjectFault(FaultMode.ShortCircuit);
            low.SetPower(true);
            high.SetPower(true);
            low.Step(0.1f);
            high.Step(0.1f);
            float currentRatio = high.Sample.Current / Math.Max(0.001f, low.Sample.Current);
            float powerRatio = high.Sample.HeatingPower / Math.Max(0.001f, low.Sample.HeatingPower);
            Require(currentRatio > 1.9f && currentRatio < 2.1f, "current ratio should be approximately 2 (actual " + currentRatio.ToString("F2", CultureInfo.InvariantCulture) + ")");
            Require(powerRatio > 3.6f && powerRatio < 4.4f, "heating ratio should be approximately 4 (actual " + powerRatio.ToString("F2", CultureInfo.InvariantCulture) + ")");
        }

        private static void TestEnergyAndCooling()
        {
            var model = new JouleModel();
            model.InjectFault(FaultMode.ShortCircuit);
            model.SetPower(true);
            float expectedElectricalEnergy = 0f;
            float lostHeat = 0f;
            for (int i = 0; i < 100; i++)
            {
                expectedElectricalEnergy += model.Sample.HeatingPower * 0.02f;
                lostHeat += model.HeatLossCoefficient * (model.Sample.Temperature - JouleModel.AmbientTemperature) * 0.02f;
                model.Step(0.02f);
            }

            float hotTemperature = model.Sample.Temperature;
            float hotEnergy = model.Sample.HeatEnergy;
            float retainedHeat = (hotTemperature - JouleModel.AmbientTemperature) * model.ThermalCapacity;
            Require(Math.Abs(hotEnergy - expectedElectricalEnergy) < 0.5f, "Q must equal integrated electrical I squared R dt");
            Require(Math.Abs(retainedHeat + lostHeat - hotEnergy) < 0.5f, "electrical input must balance temperature rise and cooling before ignition");
            model.SetPower(false);
            model.Step(5f);
            Require(hotTemperature > JouleModel.AmbientTemperature, "short circuit should heat the specimen");
            Require(model.Sample.Temperature < hotTemperature, "temperature should fall when power is isolated");
            Require(Math.Abs(model.Sample.HeatEnergy - hotEnergy) < 0.001f, "cumulative electrical Q must remain constant after isolation");
            Require(model.Sample.Current == 0f && model.Sample.HeatingPower == 0f, "isolation immediately zeros current and electric heating");
        }

        private static void TestBreakerAndUnprotected()
        {
            var protectedModel = new JouleModel();
            protectedModel.Reset(new JouleParameters { ProtectionEnabled = true });
            protectedModel.InjectFault(FaultMode.ShortCircuit);
            protectedModel.SetPower(true);
            protectedModel.Step(0.5f);

            var unprotectedModel = new JouleModel();
            unprotectedModel.InjectFault(FaultMode.ShortCircuit);
            unprotectedModel.SetPower(true);
            unprotectedModel.Step(0.5f);
            Require(protectedModel.Sample.BreakerTripped, "protected short circuit should trip");
            Require(!protectedModel.Sample.Powered, "tripped model must be unpowered");
            Require(!unprotectedModel.Sample.BreakerTripped && unprotectedModel.Sample.Powered, "unprotected model should continue powering the fault");
            Require(protectedModel.Sample.Temperature < 40f, "protection must interrupt before damaging temperature");
            protectedModel.SetPower(true);
            Require(!protectedModel.Sample.Powered, "tripped breaker cannot be re-energized without reset");
            protectedModel.Step(20f);
            unprotectedModel.Step(20f);
            Require(protectedModel.Sample.Stage == MaterialStage.Normal && unprotectedModel.Burning, "protected and unprotected trajectories must diverge before fire");
        }

        private static void TestStagesAndReset()
        {
            var model = new JouleModel();
            model.InjectFault(FaultMode.ShortCircuit);
            model.SetPower(true);
            var stages = new HashSet<MaterialStage> { model.Sample.Stage };
            for (int i = 0; i < 1250 && !model.Burning; i++)
            {
                model.Step(0.02f);
                stages.Add(model.Sample.Stage);
            }

            Require(model.Burning && model.Sample.Time >= 10f && model.Sample.Time <= 25f, "default short should ignite in 10-25 simulation seconds");
            Require(stages.Contains(MaterialStage.Warming) && stages.Contains(MaterialStage.Softened) && stages.Contains(MaterialStage.Pyrolysis), "must traverse all visual precursor stages");
            model.SetPower(false);
            model.Step(0.5f);
            Require(model.Burning && !model.Sample.Powered, "chemical burning can continue after electrical isolation");
            model.Extinguish();
            model.Step(1f);
            Require(!model.Burning && model.Sample.Stage == MaterialStage.Charred, "extinguishing leaves irreversible charred damage without re-ignition");
            model.Reset(model.Parameters);
            Require(model.Sample.Stage == MaterialStage.Normal && model.Sample.Time < 0.0001f && !model.Burning, "reset should clear thermal and visual state");
            Require(model.Sample.Fault == FaultMode.None && !model.Sample.BreakerTripped && !model.Sample.Powered && model.Sample.HeatEnergy == 0f, "reset clears faults, breaker, power and integrated Q");
        }

        private static void TestWireAreaTrend()
        {
            var thin = new JouleModel();
            thin.Reset(new JouleParameters { WireArea = 0.6f });
            thin.InjectFault(FaultMode.ShortCircuit);
            thin.SetPower(true);
            thin.Step(2f);

            var thick = new JouleModel();
            thick.Reset(new JouleParameters { WireArea = 3f });
            thick.InjectFault(FaultMode.ShortCircuit);
            thick.SetPower(true);
            thick.Step(2f);
            Require(thin.Sample.Temperature > thick.Sample.Temperature, "smaller cross-section should heat more quickly");
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException(detail);
            }
        }

        private static void TestNormalCircuit()
        {
            var model = new JouleModel();
            model.SetPower(true);
            float initialResistance = model.WireResistance;
            Require(Math.Abs(model.Sample.Current - 24f / model.TotalResistance) < 0.0001f, "current must use total finite series resistance");
            model.Step(60f);
            Require(model.Sample.Temperature < 30f && model.Sample.Stage == MaterialStage.Normal, "normal default load must remain benign");
            Require(model.WireResistance > initialResistance, "copper resistance rises with temperature");
            model.Reset(new JouleParameters { WireArea = float.NaN, LoadResistance = -1f, ContactResistance = float.PositiveInfinity });
            model.InjectFault(FaultMode.ShortCircuit);
            model.SetPower(true);
            Require(!float.IsNaN(model.Sample.Current) && !float.IsInfinity(model.Sample.Current), "invalid parameters must not produce non-finite current");
        }

        private static void TestDeterminism()
        {
            var first = new JouleModel();
            var second = new JouleModel();
            first.SetPower(true);
            second.SetPower(true);
            for (int i = 0; i < 1200; i++)
            {
                if (i == 50) { first.InjectFault(FaultMode.ShortCircuit); second.InjectFault(FaultMode.ShortCircuit); }
                if (i == 1000) { first.SetPower(false); second.SetPower(false); }
                first.Step(0.02f);
                second.Step(0.02f);
                Require(first.Sample.Time == second.Sample.Time && first.Sample.Temperature == second.Sample.Temperature
                    && first.Sample.HeatEnergy == second.Sample.HeatEnergy && first.Sample.Stage == second.Sample.Stage,
                    "same fixed-step commands must yield identical samples");
            }
        }

        private static void TestPoorContact()
        {
            var model = new JouleModel();
            model.Reset(new JouleParameters { ProtectionEnabled = true });
            model.InjectFault(FaultMode.PoorContact);
            model.SetPower(true);
            for (int i = 0; i < 1500 && !model.Burning; i++) model.Step(0.02f);
            Require(model.Burning, "small poor-contact specimen should demonstrate hotspot ignition");
            Require(model.Sample.Current < JouleModel.BreakerTripCurrent && !model.Sample.BreakerTripped,
                "ordinary overcurrent protection cannot promise to detect below-rating contact overheating");
        }

        private static void TestManagerLifecycle()
        {
            WithManager(manager =>
            {
                manager.TogglePower();
                Require(!manager.PowerOn, "power requires inspection");
                manager.SetWireArea(2.5f);
                manager.SetLoadResistance(6f);
                manager.Inspect();
                manager.TogglePower();
                Require(manager.PowerOn && manager.Phase == ExperimentPhase.Running, "inspection permits normal operation");
                var expected = new JouleModel();
                expected.Reset(manager.Parameters);
                expected.SetPower(true);
                Require(Math.Abs(manager.Sample.Current - expected.Sample.Current) < 0.0001f, "selected parameters must be applied to the model before power");
                Advance(manager, 10);
                float time = manager.Sample.Time;
                manager.SimulationSpeed = 0f;
                Advance(manager, 10);
                Require(manager.Sample.Time == time, "speed zero must pause simulation");
                manager.SimulationSpeed = 1f;
                manager.BeginReview();
                Require(!manager.IsReviewing, "review cannot start while powered");
                manager.IsolatePower();
                float isolatedTemperature = manager.Sample.Temperature;
                Advance(manager, 10);
                Require(manager.Sample.Temperature < isolatedTemperature, "manager must continue cooling after isolation");
                manager.BeginReview();
                Require(manager.IsReviewing, "safe stopped normal run permits review");
                int samples = manager.History.Count;
                JouleSample live = manager.Sample;
                manager.TogglePower();
                manager.TriggerFault(FaultMode.ShortCircuit);
                manager.IsolatePower();
                manager.RaiseAlarm();
                manager.Extinguish();
                manager.SetSprayInput(true, true);
                manager.Inspect();
                manager.SetWireArea(0.5f);
                manager.SetProtection(true);
                Advance(manager, 5);
                Require(manager.IsReviewing && !manager.PowerOn && manager.Sample.Time == live.Time
                    && manager.Sample.Fault == live.Fault && manager.Parameters.WireArea == 2.5f
                    && !manager.Parameters.ProtectionEnabled && manager.History.Count == samples
                    && !manager.SprayActive && manager.ExtinguishProgress == 0f,
                    "review must not mutate the live model, setup, or record buffer");
                manager.SetReplayNormalized(1f);
                Require(manager.DisplaySample.Time == manager.History[manager.History.Count - 1].Time, "timeline must reach final recorded sample");
                manager.EndReview();
                Require(!manager.IsReviewing && manager.Phase == ExperimentPhase.Completed, "ending review restores completed phase");
            });
        }

        private static void TestManagerFireWorkflow()
        {
            WithManager(manager =>
            {
                manager.Inspect();
                manager.TogglePower();
                manager.TriggerFault(FaultMode.ShortCircuit);
                Advance(manager, 900);
                Require(manager.Sample.Stage == MaterialStage.Burning && manager.Phase == ExperimentPhase.Incident, "incident phase must keep advancing to visible fire");
                manager.SetWireArea(3f);
                Require(manager.Parameters.WireArea == 1.5f, "setup locks during a run");
                manager.RaiseAlarm();
                manager.Extinguish();
                manager.SetSprayInput(true, true);
                Advance(manager, 10);
                Require(!manager.AlarmRaised && manager.Sample.Stage == MaterialStage.Burning
                    && !manager.SprayActive && manager.ExtinguishProgress == 0f,
                    "unsafe extinguishing/alarm order rejected before isolation");
                manager.IsolatePower();
                Advance(manager, 10);
                Require(!manager.PowerOn && manager.Sample.Current == 0f && manager.Sample.Stage == MaterialStage.Burning, "isolated burning remains visible until treated");
                manager.Extinguish();
                manager.SetSprayInput(true, true);
                Advance(manager, 10);
                Require(manager.Sample.Stage == MaterialStage.Burning && manager.ExtinguishProgress == 0f
                    && !manager.SprayActive, "alarm required before extinguisher");
                manager.RaiseAlarm();
                manager.Extinguish();
                Require(manager.Sample.Stage == MaterialStage.Burning && manager.ExtinguishProgress == 0f,
                    "single-click legacy command must not bypass physical spray interaction");
                manager.SetSprayInput(true, false);
                Advance(manager, 20);
                Require(manager.SprayActive && !manager.SprayOnTarget && manager.ExtinguishProgress == 0f,
                    "holding trigger away from fire produces no suppression progress");
                manager.SetSprayInput(true, true);
                Advance(manager, 25);
                float progress = manager.ExtinguishProgress;
                Require(progress > 0.1f && progress < 0.3f && manager.Sample.Stage == MaterialStage.Burning,
                    "a brief aimed spray only partially suppresses the fire");
                manager.SetSprayInput(false, true);
                Advance(manager, 20);
                Require(!manager.SprayActive && manager.ExtinguishProgress == progress,
                    "release immediately stops spray without advancing accumulated progress");
                manager.SetSprayInput(true, false);
                Advance(manager, 20);
                Require(manager.ExtinguishProgress == progress, "moving off target pauses accumulated suppression");
                manager.SetSprayInput(true, true);
                manager.SimulationSpeed = 0f;
                Advance(manager, 50);
                Require(manager.ExtinguishProgress == progress && manager.Sample.Stage == MaterialStage.Burning,
                    "paused simulation must not accumulate suppression");
                manager.SimulationSpeed = 1f;
                manager.SetSprayInput(true, true);
                Advance(manager, 100);
                Require(manager.Sample.Stage == MaterialStage.Burning && manager.ExtinguishProgress < 1f,
                    "less than three cumulative on-target seconds must not complete extinguishing");
                Advance(manager, 30);
                Require(manager.AlarmRaised && manager.Phase == ExperimentPhase.Completed && manager.Sample.Stage == MaterialStage.Charred, "correct safety sequence completes experiment");
                Require(manager.ExtinguishProgress == 1f && !manager.SprayActive,
                    "completed extinguishing reports full progress and stops emission");
                int count = manager.History.Count;
                int resetVersion = manager.ResetVersion;
                manager.SimulationSpeed = 3f;
                manager.ResetExperiment();
                Require(manager.PreviousHistory.Count == count && manager.History.Count == 0, "reset retains previous trace and clears current trace");
                Require(manager.SimulationSpeed == 1f && !manager.PowerOn && !manager.AlarmRaised
                    && !manager.IsInspected && manager.Sample.Stage == MaterialStage.Normal
                    && manager.Sample.Fault == FaultMode.None && manager.Phase == ExperimentPhase.Inspection
                    && manager.ResetVersion > resetVersion && !manager.SprayActive && manager.ExtinguishProgress == 0f,
                    "full reset clears visible and procedural state and signals interaction adapters");
                manager.ResetExperiment();
                Require(manager.PreviousHistory.Count == count, "an empty reset must not erase the last comparison run");
            });
        }

        private static void TestFaultLocation()
        {
            WithManager(manager =>
            {
                Require(manager.CanSelectFaultLocation, "fault placement is available before power");
                Require(manager.SetFaultLocation(3, 0.371f), "arbitrary point on return wire must be selectable");
                Require(manager.Parameters.FaultWire == 3 && Math.Abs(manager.Parameters.FaultPosition - 0.371f) < 0.0001f,
                    "selection must preserve continuous position instead of snapping to presets");
                Require(!manager.SetFaultLocation(-1, 0.5f) && !manager.SetFaultLocation(4, 0.5f)
                    && !manager.SetFaultLocation(1, -0.01f) && !manager.SetFaultLocation(1, 1.01f)
                    && !manager.SetFaultLocation(1, float.NaN) && !manager.SetFaultLocation(1, float.PositiveInfinity),
                    "invalid wires and non-finite/out-of-range positions must be rejected");
                Require(manager.Parameters.FaultWire == 3 && Math.Abs(manager.Parameters.FaultPosition - 0.371f) < 0.0001f,
                    "invalid placement must preserve the last valid selection");
                manager.Inspect();
                manager.TogglePower();
                Require(!manager.CanSelectFaultLocation && !manager.SetFaultLocation(0, 0.1f),
                    "a running experiment must retain the prepared fault location");
                manager.TriggerFault(FaultMode.ShortCircuit);
                Advance(manager, 25);
                manager.IsolatePower();
                manager.RaiseAlarm();
                manager.BeginReview();
                Require(manager.IsReviewing && !manager.SetFaultLocation(2, 0.8f), "review cannot edit fault placement");
                manager.SetReplayNormalized(0f);
                Require(manager.DisplaySample.FaultWire == 3 && Math.Abs(manager.DisplaySample.FaultPosition - 0.371f) < 0.0001f,
                    "first replay sample stores the selected wire and exact relative position");
                manager.SetReplayNormalized(1f);
                Require(manager.DisplaySample.FaultWire == 3 && Math.Abs(manager.DisplaySample.FaultPosition - 0.371f) < 0.0001f,
                    "final replay sample retains fault placement");
                manager.ResetExperiment();
                Require(manager.SetFaultLocation(0, 0.817f), "reset permits a different location for the next run");
                Require(manager.PreviousHistory.Count > 0, "previous trace survives reset");
                foreach (JouleSample sample in manager.PreviousHistory)
                {
                    Require(sample.FaultWire == 3 && Math.Abs(sample.FaultPosition - 0.371f) < 0.0001f,
                        "changing next-run setup must not move the previous run's recorded hotspot");
                }
            });
        }

        private static void TestSprayReset()
        {
            WithManager(manager =>
            {
                manager.Inspect();
                manager.TogglePower();
                manager.TriggerFault(FaultMode.ShortCircuit);
                Advance(manager, 900);
                manager.IsolatePower();
                manager.RaiseAlarm();
                manager.SetSprayInput(true, true);
                Advance(manager, 25);
                Require(manager.SprayActive && manager.ExtinguishProgress > 0f, "fixture has active partial suppression");
                manager.ResetExperiment();
                Advance(manager, 200);
                Require(!manager.SprayActive && !manager.SprayOnTarget && manager.ExtinguishProgress == 0f
                    && manager.Sample.Time == 0f && manager.Sample.Stage == MaterialStage.Normal,
                    "reset must cancel a held trigger and discard its suppression progress");
            });
        }

        private static void TestManagerBreakerEvent()
        {
            WithManager(manager =>
            {
                manager.SetProtection(true);
                manager.Inspect();
                manager.TogglePower();
                manager.TriggerFault(FaultMode.ShortCircuit);
                Advance(manager, 100);
                int trips = 0;
                foreach (string entry in manager.Events) if (entry.Contains("过流保护跳闸")) trips++;
                Require(manager.Sample.BreakerTripped && !manager.PowerOn && trips == 1, "breaker transition must be recorded once while cooling continues");
            });
        }

        private static void TestWireGeometry()
        {
            for (int wire = 0; wire < WirePathGeometry.WireCount; wire++)
            {
                Vector3[] vertices = WirePathGeometry.Points(wire);
                Require(Vector3.Distance(WirePathGeometry.Point(wire, 0f), vertices[0]) < 0.00001f
                    && Vector3.Distance(WirePathGeometry.Point(wire, 1f), vertices[vertices.Length - 1]) < 0.00001f,
                    "continuous selection includes both ends of every selectable wire");
                foreach (float position in new[] { .173f, .371f, .817f })
                {
                    Vector3 point = WirePathGeometry.Point(wire, position);
                    Require(WirePathGeometry.ClosestPoint(point, out int foundWire, out float foundPosition, out Vector3 snapped, .001f)
                        && foundWire == wire && Math.Abs(foundPosition - position) < .0001f
                        && Vector3.Distance(point, snapped) < .0001f,
                        "world-independent path queries preserve arbitrary continuous positions on each wire");
                }
            }
            Require(!WirePathGeometry.ClosestPoint(new Vector3(3f, 1f, 3f), out _, out _, out _, .12f)
                && !WirePathGeometry.ClosestPoint(new Vector3(float.NaN, 1f, 0f), out _, out _, out _, .12f),
                "empty space and invalid tracking coordinates must not select an electrical fault");
        }

        private static void TestToolInteraction()
        {
            WithManager(manager =>
            {
                var bench = new GameObject("SpatialTestBench");
                bench.transform.SetParent(manager.transform, false);
                bench.transform.SetPositionAndRotation(new Vector3(2f, .2f, -1f), Quaternion.Euler(0f, 27f, 0f));
                var body = new GameObject("SpatialTestExtinguisher");
                body.transform.SetParent(manager.transform, false);
                ExtinguisherInteraction interaction = body.AddComponent<ExtinguisherInteraction>();
                Vector3 home = new Vector3(1f, .6f, -1f);
                body.transform.localPosition = home;
                var horn = new GameObject("Horn");
                horn.transform.SetParent(body.transform, false);
                horn.transform.localPosition = Vector3.up * .2f;
                var outlet = new GameObject("Outlet");
                outlet.transform.SetParent(horn.transform, false);
                outlet.transform.localPosition = Vector3.forward * .17f;
                interaction.Configure(manager, bench.transform, horn.transform, outlet.transform, null, null, null, null, null);
                manager.SetFaultLocation(0, .63f);
                manager.Inspect();
                manager.TogglePower();
                manager.TriggerFault(FaultMode.ShortCircuit);
                Advance(manager, 900);
                manager.IsolatePower();
                manager.RaiseAlarm();
                Vector3 target = bench.transform.TransformPoint(WirePathGeometry.Point(0, .63f) + Vector3.up * .035f);
                Vector3 workingPosition = target + new Vector3(0f, .2f, -1.5f);
                interaction.MoveTo(workingPosition);
                Require(body.transform.localPosition == home, "unheld tools do not follow move commands");
                interaction.Grab();
                interaction.MoveTo(workingPosition);
                interaction.AimAt(target);
                Require(interaction.IsHeld && Vector3.Distance(body.transform.position, workingPosition) < .0001f
                    && interaction.IsOnTarget, "world-space move and aim operate correctly in a relocated, rotated laboratory");
                interaction.MoveTo(new Vector3(float.NaN, 0f, 0f));
                Require(Vector3.Distance(body.transform.position, workingPosition) < .0001f,
                    "invalid tracking positions cannot corrupt the held tool pose");
                interaction.AimAt(target + Vector3.right * 3f);
                interaction.SetTrigger(true);
                Advance(manager, 25);
                Require(!interaction.IsOnTarget && manager.ExtinguishProgress == 0f,
                    "a held switch while the nozzle points away must not suppress fire");
                interaction.MoveTo(target + Vector3.back * 4f);
                interaction.AimAt(target);
                interaction.SetTrigger(true);
                Advance(manager, 25);
                Require(!interaction.IsOnTarget && manager.ExtinguishProgress == 0f,
                    "aiming from outside effective range must not suppress fire");
                interaction.MoveTo(workingPosition);
                interaction.AimAt(target);
                interaction.SetTrigger(true);
                Advance(manager, 25);
                float partialProgress = manager.ExtinguishProgress;
                Require(interaction.IsOnTarget && manager.SprayActive && partialProgress > 0f,
                    "valid pose and held trigger feed effective suppression through the shared interface");
                interaction.Release();
                Advance(manager, 25);
                Require(!interaction.IsHeld && !manager.SprayActive && manager.ExtinguishProgress == partialProgress
                    && Vector3.Distance(body.transform.position, workingPosition) < .0001f,
                    "release stops spray while preserving the chosen placement");
                interaction.ResetPose();
                Require(body.transform.localPosition == home && !interaction.IsHeld && !manager.SprayActive,
                    "explicit return restores the saved home pose and stops interaction");
                interaction.Grab();
                interaction.MoveTo(workingPosition);
                interaction.AimAt(target);
                interaction.SetTrigger(true);
                manager.ResetExperiment();
                MethodInfo update = typeof(ExtinguisherInteraction).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
                Require(update != null, "tool update must synchronize with experiment reset");
                update.Invoke(interaction, null);
                Require(body.transform.localPosition == home && !interaction.IsHeld && !interaction.IsOnTarget
                    && !manager.SprayActive && manager.ExtinguishProgress == 0f,
                    "experiment reset returns a moved tool and clears any held switch state");
                manager.Inspect();
                manager.TogglePower();
                Advance(manager, 10);
                manager.IsolatePower();
                manager.BeginReview();
                interaction.Grab();
                Require(manager.IsReviewing && !interaction.IsHeld, "review prevents live tool grabs");
            });
        }

        private static void TestCsvExport()
        {
            WithManager(manager =>
            {
                manager.SetFaultLocation(2, 0.631f);
                manager.Inspect();
                manager.TogglePower();
                Advance(manager, 20);
                manager.IsolatePower();
                CultureInfo originalCulture = CultureInfo.CurrentCulture;
                string path = string.Empty;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                    path = manager.ExportCsv();
                    Require(!string.IsNullOrEmpty(path) && File.Exists(path), "export must create a results CSV");
                    Require(Path.GetFullPath(Path.GetDirectoryName(path)) == Path.GetFullPath(Path.Combine(Application.persistentDataPath, "Results")), "export uses persistent results directory");
                    string[] lines = File.ReadAllLines(path);
                    Require(lines.Length == manager.History.Count + 1 && lines[0].Contains("ElectricalHeat_J"), "CSV includes units and all current samples");
                    string[] row = lines[1].Split(',');
                    Require(row.Length == 16 && float.TryParse(row[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _), "CSV must remain parseable independent of locale");
                    string[] headers = lines[0].Split(',');
                    int wireColumn = Array.IndexOf(headers, "FaultWire");
                    int positionColumn = Array.IndexOf(headers, "FaultPosition");
                    Require(wireColumn >= 0 && positionColumn >= 0, "CSV records the selected wire and normalized position");
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string[] values = lines[i].Split(',');
                        JouleSample sample = manager.History[i - 1];
                        Require(int.Parse(values[wireColumn], CultureInfo.InvariantCulture) == sample.FaultWire
                            && Math.Abs(float.Parse(values[positionColumn], CultureInfo.InvariantCulture) - sample.FaultPosition) < 0.001f,
                            "every CSV row must retain its corresponding sample's location");
                    }
                }
                finally
                {
                    CultureInfo.CurrentCulture = originalCulture;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
                }
            });
        }

        private static void WithManager(Action<JouleExperimentManager> test)
        {
            var container = new GameObject("JouleTestFixture") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                JouleExperimentManager manager = container.AddComponent<JouleExperimentManager>();
                manager.ResetExperiment();
                test(manager);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        private static void Advance(JouleExperimentManager manager, int steps)
        {
            MethodInfo frame = typeof(JouleExperimentManager).GetMethod("AdvanceFrame", BindingFlags.NonPublic | BindingFlags.Instance);
            Require(frame != null, "manager fixed-step frame entry point must exist");
            for (int i = 0; i < steps; i++) frame.Invoke(manager, new object[] { 0.02f });
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private readonly struct TestResult
        {
            public readonly string Name;
            public readonly bool Passed;
            public readonly string Detail;

            public TestResult(string name, bool passed, string detail)
            {
                Name = name;
                Passed = passed;
                Detail = detail;
            }
        }
    }
}
