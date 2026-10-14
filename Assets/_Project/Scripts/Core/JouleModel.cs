using System;

namespace MRLab.Joule
{
    /// <summary>
    /// Fault topology used by the PC teaching experiment.
    /// </summary>
    public enum FaultMode
    {
        None,
        ShortCircuit,
        PoorContact
    }

    /// <summary>
    /// Thermal state of the deliberately small, lumped wire/contact specimen.
    /// Warming is the visual discolouration stage used by the PC presentation.
    /// </summary>
    public enum MaterialStage
    {
        Normal,
        Warming,
        Softened,
        Pyrolysis,
        Burning,
        Charred
    }

    [Serializable]
    public class JouleParameters
    {
        public float Voltage = 24f;
        public float WireArea = 1.5f;
        public float LoadResistance = 12f;
        public float ContactResistance = 0.08f;
        public bool ProtectionEnabled;
        /// <summary>
        /// Wire that receives the deliberately injected fault.
        /// 0 = supply positive, 1 = test wire, 2 = load positive, 3 = return.
        /// </summary>
        public int FaultWire = 1;

        /// <summary>
        /// Normalized distance along <see cref="FaultWire"/>. It is kept in the
        /// simulation data so recorded/replayed hotspots do not depend on live UI state.
        /// </summary>
        public float FaultPosition = 0.5f;

        public JouleParameters Clone()
        {
            return new JouleParameters
            {
                Voltage = Voltage,
                WireArea = WireArea,
                LoadResistance = LoadResistance,
                ContactResistance = ContactResistance,
                ProtectionEnabled = ProtectionEnabled,
                FaultWire = FaultWire,
                FaultPosition = FaultPosition
            };
        }

        internal void Sanitize()
        {
            Voltage = ClampFinite(Voltage, 0f, 240f, 24f);
            WireArea = ClampFinite(WireArea, 0.1f, 50f, 1.5f);
            LoadResistance = ClampFinite(LoadResistance, 0.05f, 1000f, 12f);
            ContactResistance = ClampFinite(ContactResistance, 0f, 100f, 0.08f);
            FaultWire = Math.Max(0, Math.Min(3, FaultWire));
            FaultPosition = ClampFinite(FaultPosition, 0f, 1f, 0.5f);
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Math.Max(min, Math.Min(max, value));
        }
    }

    [Serializable]
    public struct JouleSample
    {
        public float Time;
        public float Current;
        public float Temperature;
        public float HeatEnergy;
        public float HeatingPower;
        public MaterialStage Stage;
        public bool Powered;
        public bool BreakerTripped;
        public FaultMode Fault;
        public int FaultWire;
        public float FaultPosition;

        public JouleSample(
            float time,
            float current,
            float temperature,
            float heatEnergy,
            float heatingPower,
            MaterialStage stage,
            bool powered,
            bool breakerTripped,
            FaultMode fault,
            int faultWire,
            float faultPosition)
        {
            Time = time;
            Current = current;
            Temperature = temperature;
            HeatEnergy = heatEnergy;
            HeatingPower = heatingPower;
            Stage = stage;
            Powered = powered;
            BreakerTripped = breakerTripped;
            Fault = fault;
            FaultWire = faultWire;
            FaultPosition = faultPosition;
        }
    }

    /// <summary>
    /// Deterministic lumped circuit and thermal model. It intentionally contains
    /// no Unity types, input handling, rendering code, or hardware assumptions.
    /// </summary>
    public sealed class JouleModel
    {
        public const float AmbientTemperature = 22f;
        public const float CopperResistivityAtAmbient = 0.0175f; // ohm mm2 / m
        public const float CopperTemperatureCoefficient = 0.00393f;
        public const float SourceResistance = 0.35f;

        private const float WireLength = 2f;
        public const float BreakerTripCurrent = 10f;
        public const float BreakerTripDelay = 0.12f;
        private const float IntegrationStep = 0.02f;

        private bool _powered;
        private bool _breakerTripped;
        private float _time;
        private float _temperature = AmbientTemperature;
        private float _breakerOvercurrentTime;
        private float _electricalEnergy;
        private float _warmingDwell;
        private float _softeningDwell;
        private float _pyrolysisDwell;
        private float _ignitionDwell;
        private float _burningTime;
        private bool _extinguished;
        private FaultMode _fault;
        private MaterialStage _stage = MaterialStage.Normal;

        public JouleParameters Parameters { get; private set; }
        public JouleSample Sample { get; private set; }
        public bool Burning { get; private set; }

        /// <summary>Useful for a panel that wants to explain the circuit reading.</summary>
        public float WireResistance => GetWireResistance(_temperature);

        /// <summary>The resistance currently in series with the ideal source.</summary>
        public float TotalResistance => GetTotalResistance();

        /// <summary>Electrical resistance whose I squared R is the displayed local power.</summary>
        public float HeatedResistance => GetHeatedResistance();

        /// <summary>Effective lumped sample heat capacity in J/K, not a whole cable rating.</summary>
        public float ThermalCapacity => _fault == FaultMode.PoorContact
            ? 0.18f + 0.04f * Parameters.WireArea
            : 8f + 4f * Parameters.WireArea;

        /// <summary>Heat-loss coefficient in W/K; small contacts cool poorly.</summary>
        public float HeatLossCoefficient => _fault == FaultMode.PoorContact
            ? 0.009f
            : 0.16f * (float)Math.Sqrt(Parameters.WireArea / 1.5f);

        public JouleModel()
        {
            Reset(new JouleParameters());
        }

        public void Reset(JouleParameters parameters)
        {
            Parameters = (parameters ?? new JouleParameters()).Clone();
            Parameters.Sanitize();
            _powered = false;
            _breakerTripped = false;
            _time = 0f;
            _temperature = AmbientTemperature;
            _breakerOvercurrentTime = 0f;
            _electricalEnergy = 0f;
            _warmingDwell = 0f;
            _softeningDwell = 0f;
            _pyrolysisDwell = 0f;
            _ignitionDwell = 0f;
            _burningTime = 0f;
            _extinguished = false;
            _fault = FaultMode.None;
            _stage = MaterialStage.Normal;
            Burning = false;
            RefreshSample();
        }

        public void SetPower(bool powered)
        {
            if (_breakerTripped)
            {
                _powered = false;
                RefreshSample();
                return;
            }

            _powered = powered;
            RefreshSample();
        }

        public void InjectFault(FaultMode fault)
        {
            _fault = Enum.IsDefined(typeof(FaultMode), fault) ? fault : FaultMode.None;
            RefreshSample();
        }

        /// <summary>
        /// Stops the flame visual state while preserving the specimen's thermal state.
        /// The manager is responsible for the required power isolation/alarm guard.
        /// </summary>
        public void Extinguish()
        {
            _extinguished = true;
            if (Burning || _stage == MaterialStage.Burning)
            {
                _stage = MaterialStage.Charred;
            }

            Burning = false;
            RefreshSample();
        }

        public void Step(float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f)
            {
                return;
            }

            // A bounded deterministic substep keeps replay stable even if a frame stalls.
            double remaining = dt;
            while (remaining > 0.0000001)
            {
                float step = (float)Math.Min(remaining, IntegrationStep);
                StepSubstep(step);
                remaining -= step;
            }
        }

        private void StepSubstep(float dt)
        {
            float resistance = GetTotalResistance();
            float current = _powered && !_breakerTripped
                ? Parameters.Voltage / Math.Max(0.01f, resistance)
                : 0f;

            if (_powered && !_breakerTripped && Parameters.ProtectionEnabled && current >= BreakerTripCurrent)
            {
                _breakerOvercurrentTime += dt;
                if (_breakerOvercurrentTime >= BreakerTripDelay)
                {
                    _breakerTripped = true;
                    _powered = false;
                    current = 0f;
                }
            }
            else if (current < BreakerTripCurrent)
            {
                _breakerOvercurrentTime = 0f;
            }

            float generation = current * current * GetHeatedResistance();
            float cooling = HeatLossCoefficient * Math.Max(0f, _temperature - AmbientTemperature);
            // Once insulation ignites, chemical heat continues after electrical isolation.
            // Its finite fuel supply is a teaching approximation, not electrical Q.
            float combustion = Burning ? (_fault == FaultMode.PoorContact ? 8f : 100f) : 0f;
            _electricalEnergy += generation * dt;
            _temperature += (generation + combustion - cooling) * dt / ThermalCapacity;
            _temperature = Math.Max(AmbientTemperature, Math.Min(1200f, _temperature));
            _time += dt;
            UpdateStage(dt);
            RefreshSample();
        }

        private void RefreshSample()
        {
            float current = _powered && !_breakerTripped ? Parameters.Voltage / GetTotalResistance() : 0f;
            float heatingPower = current * current * GetHeatedResistance();
            Sample = new JouleSample(
                _time,
                Math.Max(0f, current),
                _temperature,
                _electricalEnergy,
                Math.Max(0f, heatingPower),
                _stage,
                _powered && !_breakerTripped,
                _breakerTripped,
                _fault,
                Parameters.FaultWire,
                Parameters.FaultPosition);
        }

        private float GetWireResistance(float temperature)
        {
            float area = Math.Max(0.1f, Parameters.WireArea);
            float rho = CopperResistivityAtAmbient * (1f + CopperTemperatureCoefficient * (temperature - 20f));
            return Math.Max(0.0001f, rho * WireLength / area);
        }

        private float GetTotalResistance()
        {
            float wire = GetWireResistance(_temperature);
            float contact = Parameters.ContactResistance;

            switch (_fault)
            {
                case FaultMode.ShortCircuit:
                    // The load is bypassed, but finite source, copper, and contact
                    // resistance keep current physically bounded for the demo.
                    return SourceResistance + wire + Math.Max(0.005f, contact) + 0.025f;
                case FaultMode.PoorContact:
                    // A poor plug/contact adds a local hotspot resistance in series.
                    return SourceResistance + wire + Math.Max(0.01f, contact * 20f) + Parameters.LoadResistance;
                default:
                    return SourceResistance + wire + contact + Parameters.LoadResistance;
            }
        }

        private float GetHeatedResistance()
        {
            switch (_fault)
            {
                case FaultMode.ShortCircuit:
                    return GetWireResistance(_temperature) + Math.Max(0.005f, Parameters.ContactResistance);
                case FaultMode.PoorContact:
                    // Poor contact multiplies the set contact resistance by 20.
                    // Displayed P is local loss, not useful load power or source loss.
                    return GetWireResistance(_temperature) + Math.Max(0.01f, Parameters.ContactResistance * 20f);
                default:
                    return GetWireResistance(_temperature) + Parameters.ContactResistance;
            }
        }

        private void UpdateStage(float dt)
        {
            if (Burning)
            {
                _burningTime += dt;
                // Leave time for learners to grab, reposition and aim a spatial tool.
                // This finite 180-second training window is not a real material burn-time claim.
                if (_burningTime >= 180f)
                {
                    _stage = MaterialStage.Charred;
                    Burning = false;
                }

                return;
            }

            if (_stage == MaterialStage.Charred || _extinguished)
            {
                return;
            }

            _warmingDwell = _temperature >= 60f ? _warmingDwell + dt : 0f;
            _softeningDwell = _temperature >= 120f ? _softeningDwell + dt : 0f;
            _pyrolysisDwell = _temperature >= 220f ? _pyrolysisDwell + dt : 0f;
            _ignitionDwell = _temperature >= 300f ? _ignitionDwell + dt : 0f;
            if (_ignitionDwell >= 1.2f)
            {
                _stage = MaterialStage.Burning;
                Burning = true;
                _burningTime = 0f;
            }
            else if (_pyrolysisDwell >= 0.7f && _stage < MaterialStage.Pyrolysis)
            {
                _stage = MaterialStage.Pyrolysis;
            }
            else if (_softeningDwell >= 0.5f && _stage < MaterialStage.Softened)
            {
                _stage = MaterialStage.Softened;
            }
            else if (_warmingDwell >= 0.2f && _stage < MaterialStage.Warming)
            {
                _stage = MaterialStage.Warming;
            }
            else if (_temperature < 50f && _stage == MaterialStage.Warming)
            {
                _stage = MaterialStage.Normal;
            }
        }
    }
}
