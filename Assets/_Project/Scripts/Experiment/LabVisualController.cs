using TMPro;
using UnityEngine;

namespace MRLab.Joule
{
    /// <summary>Maps experiment measurements to the reusable laboratory visuals.</summary>
    public sealed class LabVisualController : MonoBehaviour
    {
        [SerializeField] private Transform focusPoint;
        [SerializeField] private Renderer[] wireRenderers;
        [SerializeField] private Renderer powerIndicator;
        [SerializeField] private Renderer breakerIndicator;
        [SerializeField] private Renderer hotspotRenderer;
        [SerializeField] private GameObject faultJumper;
        [SerializeField] private Transform breakerLever;
        [SerializeField] private TMP_Text powerReadout;
        [SerializeField] private TMP_Text temperatureReadout;
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private ParticleSystem flame;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private Light hotspotLight;
        [SerializeField] private GameObject thermalMarker;

        [SerializeField] private JouleExperimentManager experiment;
        private MaterialPropertyBlock properties;
        private bool hasRendered;
        private bool lastSmoke;
        private bool lastFlame;
        private bool lastSparks;
        private float nextReadoutUpdate;
        private float lastSampleTime = -1f;

        public Transform FocusPoint => focusPoint;

        public void Bind(JouleExperimentManager manager)
        {
            experiment = manager;
            if (experiment != null)
                RenderSample(experiment.DisplaySample, experiment.ThermalOverlay);
        }

        private void Awake()
        {
            if (experiment == null)
                experiment = FindObjectOfType<JouleExperimentManager>();
        }

        public void Configure(Transform focus, Renderer[] conductors, Renderer power,
            Renderer breaker, Renderer hotSpot, GameObject jumper, Transform lever,
            TMP_Text supplyText, TMP_Text temperatureText, ParticleSystem smokeSystem,
            ParticleSystem flameSystem, ParticleSystem sparkSystem, Light glow,
            GameObject thermal)
        {
            focusPoint = focus;
            wireRenderers = conductors;
            powerIndicator = power;
            breakerIndicator = breaker;
            hotspotRenderer = hotSpot;
            faultJumper = jumper;
            breakerLever = lever;
            powerReadout = supplyText;
            temperatureReadout = temperatureText;
            smoke = smokeSystem;
            flame = flameSystem;
            sparks = sparkSystem;
            hotspotLight = glow;
            thermalMarker = thermal;
        }

        private void Update()
        {
            if (experiment != null)
                RenderSample(experiment.DisplaySample, experiment.ThermalOverlay);
        }

        public void RenderSample(JouleSample sample, bool thermalOverlay)
        {
            if (properties == null)
                properties = new MaterialPropertyBlock();

            float temperature = (float)sample.Temperature;
            float warmth = Mathf.InverseLerp(60f, 360f, temperature);
            int position = experiment == null || experiment.Parameters == null
                ? 1 : Mathf.Clamp(experiment.Parameters.FaultPosition, 0, 2);
            float hotX = position == 0 ? -.13f : position == 2 ? .18f : .03f;
            SetHotspotPosition(hotX);
            bool reset = sample.Time <= .001f && (lastSampleTime > .001f || !hasRendered);
            if (reset)
            {
                ClearEffect(smoke);
                ClearEffect(flame);
                ClearEffect(sparks);
                lastSmoke = lastFlame = lastSparks = false;
                hasRendered = false;
                nextReadoutUpdate = 0f;
            }
            bool smoking = sample.Stage == MaterialStage.Pyrolysis || sample.Stage == MaterialStage.Burning;
            bool burning = sample.Stage == MaterialStage.Burning;
            bool sparking = sample.Powered && sample.Fault == FaultMode.ShortCircuit && !sample.BreakerTripped;
            bool energized = sample.Powered && !sample.BreakerTripped;
            bool charred = sample.Stage == MaterialStage.Charred;

            Color jacket = Color.Lerp(new Color(.24f, .12f, .09f), new Color(.95f, .24f, .025f), warmth);
            if (charred)
                jacket = new Color(.045f, .04f, .04f);
            if (thermalOverlay)
                jacket = Color.Lerp(new Color(.08f, .23f, .75f), new Color(1f, .11f, .025f), warmth);
            Color radiation = warmth > .25f ? new Color(1f, .08f, .008f) * warmth * (thermalOverlay ? 2f : .7f) : Color.black;
            if (wireRenderers != null)
                foreach (Renderer wire in wireRenderers)
                {
                    if (wire == null)
                        continue;
                    float localHeat = 1f - Mathf.Clamp01(Mathf.Abs(wire.transform.localPosition.x - hotX) / .5f);
                    Color localJacket = Color.Lerp(thermalOverlay ? new Color(.08f, .23f, .75f) : new Color(.24f, .12f, .09f), jacket, localHeat);
                    SetAppearance(wire, localJacket, radiation * localHeat);
                }
            SetAppearance(hotspotRenderer, jacket, radiation * 1.3f);
            SetAppearance(powerIndicator, energized ? new Color(.05f, .8f, .52f) : new Color(.07f, .15f, .15f), energized ? new Color(.02f, 1f, .3f) : Color.black);
            SetAppearance(breakerIndicator, sample.BreakerTripped ? new Color(1f, .25f, .07f) : new Color(.12f, .48f, .32f), sample.BreakerTripped ? new Color(1f, .13f, .02f) : Color.black);
            if (faultJumper != null)
                faultJumper.SetActive(sample.Fault == FaultMode.ShortCircuit);
            if (breakerLever != null)
                breakerLever.localRotation = Quaternion.Euler(energized ? -18f : 20f, 0f, 0f);
            if (thermalMarker != null)
                thermalMarker.SetActive(thermalOverlay);
            if (hotspotLight != null)
            {
                hotspotLight.enabled = burning || warmth > .55f;
                hotspotLight.intensity = burning ? 1.6f + Mathf.Sin(Time.time * 17f) * .35f : warmth * .45f;
            }

            SetEffect(smoke, smoking, ref lastSmoke);
            SetEffect(flame, burning, ref lastFlame);
            SetEffect(sparks, sparking, ref lastSparks);
            if (!hasRendered || !Application.isPlaying || Time.unscaledTime >= nextReadoutUpdate)
            {
                if (powerReadout != null)
                    powerReadout.text = energized ? "DC  " + ((float)sample.Current).ToString("0.00") + " A" : "DC  0.00 A";
                if (temperatureReadout != null)
                    temperatureReadout.text = temperature.ToString("0.0") + " °C";
                nextReadoutUpdate = Time.unscaledTime + .1f;
            }
            hasRendered = true;
            lastSampleTime = sample.Time;
        }

        private void SetHotspotPosition(float hotX)
        {
            if (hotspotRenderer != null)
                hotspotRenderer.transform.localPosition = new Vector3(hotX, .96f, -.1f);
            if (smoke != null)
                smoke.transform.localPosition = new Vector3(hotX, .985f, -.1f);
            if (flame != null)
                flame.transform.localPosition = new Vector3(hotX, .985f, -.1f);
            if (sparks != null)
                sparks.transform.localPosition = new Vector3(hotX, .985f, -.1f);
            if (hotspotLight != null)
                hotspotLight.transform.localPosition = new Vector3(hotX, 1.105f, -.1f);
            if (thermalMarker != null)
                thermalMarker.transform.localPosition = new Vector3(hotX - .03f, 0f, 0f);
            if (focusPoint != null)
                focusPoint.localPosition = new Vector3(hotX, .99f, -.02f);
        }

        private static void ClearEffect(ParticleSystem effect)
        {
            if (effect != null)
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void SetAppearance(Renderer target, Color color, Color emission)
        {
            if (target == null)
                return;
            properties.Clear();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetColor("_EmissionColor", emission);
            target.SetPropertyBlock(properties);
        }

        private void SetEffect(ParticleSystem effect, bool active, ref bool previous)
        {
            if (effect == null || (!Application.isPlaying && !active))
                return;
            if (!hasRendered || active != previous)
            {
                if (active)
                    effect.Play(true);
                else
                    effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            previous = active;
        }
    }
}
