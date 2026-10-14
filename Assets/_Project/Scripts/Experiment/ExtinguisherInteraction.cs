using UnityEngine;

namespace MRLab.Joule
{
    /// <summary>
    /// Portable tool behaviour. Mouse and future XR grab/pose/trigger adapters call
    /// this API; experiment rules and extinguishing progress stay in the manager.
    /// </summary>
    public sealed class ExtinguisherInteraction : MonoBehaviour
    {
        [SerializeField] private JouleExperimentManager experiment;
        [SerializeField] private Transform circuitFrame;
        [SerializeField] private Transform hornPivot;
        [SerializeField] private Transform nozzle;
        [SerializeField] private Transform lever;
        [SerializeField] private ParticleSystem spray;
        [SerializeField] private LineRenderer hose;
        [SerializeField] private LineRenderer aimGuide;
        [SerializeField] private Renderer aimMarker;
        [SerializeField] private Vector3 homePosition;
        [SerializeField] private Quaternion homeRotation = Quaternion.identity;

        private Quaternion leverRest;
        private Quaternion hornRest;
        private bool trigger;
        private bool wasSpraying;
        private int resetVersion;
        private MaterialPropertyBlock markerProperties;

        public const float SprayRange = 2.5f;
        public const float SprayHalfAngle = 8f;
        public bool IsHeld { get; private set; }
        public bool IsOnTarget { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public Vector3 NozzlePosition => nozzle != null ? nozzle.position : transform.position;

        public void Configure(JouleExperimentManager manager, Transform benchFrame, Transform horn,
            Transform outlet, Transform handleLever, ParticleSystem particles, LineRenderer hoseLine,
            LineRenderer guide, Renderer marker)
        {
            experiment = manager;
            circuitFrame = benchFrame;
            hornPivot = horn;
            nozzle = outlet;
            lever = handleLever;
            spray = particles;
            hose = hoseLine;
            aimGuide = guide;
            aimMarker = marker;
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            CachePose();
            ResetPose();
        }

        private void Awake()
        {
            if (experiment == null) experiment = FindObjectOfType<JouleExperimentManager>();
            CachePose();
            resetVersion = experiment != null ? experiment.ResetVersion : 0;
            ResetPose();
        }

        private void CachePose()
        {
            if (lever != null) leverRest = lever.localRotation;
            if (hornPivot != null) hornRest = hornPivot.localRotation;
        }

        public void Grab()
        {
            if (experiment != null && experiment.IsReviewing) return;
            IsHeld = true;
            trigger = false;
            AimPoint = NozzlePosition + (nozzle != null ? nozzle.forward : transform.forward);
        }

        public void MoveTo(Vector3 worldPosition)
        {
            if (!IsHeld || !Finite(worldPosition)) return;
            // Metre-scale support surfaces are shared by all input adapters.
            // Keep the cylinder above the worktop or floor without constraining
            // the user's horizontal position or chosen aiming direction.
            Vector3 labPosition = circuitFrame != null ? circuitFrame.InverseTransformPoint(worldPosition) : worldPosition;
            bool overBench = Mathf.Abs(labPosition.x) <= 1.65f && Mathf.Abs(labPosition.z) <= .80f;
            labPosition.y = Mathf.Max(labPosition.y, overBench ? 1.16f : .43f);
            worldPosition = circuitFrame != null ? circuitFrame.TransformPoint(labPosition) : labPosition;
            transform.position = worldPosition;
            AimAt(AimPoint);
        }

        public void AimAt(Vector3 worldPoint)
        {
            if (!IsHeld || !Finite(worldPoint)) return;
            AimPoint = worldPoint;
            if (hornPivot != null && (worldPoint - hornPivot.position).sqrMagnitude > .0001f)
            {
                Vector3 direction = (worldPoint - hornPivot.position).normalized;
                Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > .99f ? Vector3.right : Vector3.up;
                hornPivot.rotation = Quaternion.LookRotation(direction, up);
            }
            UpdateTarget();
        }

        public void SetTrigger(bool pressed)
        {
            trigger = pressed && IsHeld && experiment != null && !experiment.IsReviewing;
            UpdateTarget();
            if (experiment != null) experiment.SetSprayInput(trigger, IsOnTarget);
        }

        public void Release()
        {
            SetTrigger(false);
            IsHeld = false;
            IsOnTarget = false;
            ShowAim(false);
        }

        public void ResetPose()
        {
            Release();
            transform.localPosition = homePosition;
            transform.localRotation = homeRotation;
            if (hornPivot != null) hornPivot.localRotation = hornRest;
            if (lever != null) lever.localRotation = leverRest;
            AimPoint = NozzlePosition + (nozzle != null ? nozzle.forward : transform.forward);
            if (spray != null) spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            wasSpraying = false;
            UpdateHose();
        }

        private void Update()
        {
            if (experiment == null) return;
            if (resetVersion != experiment.ResetVersion)
            {
                resetVersion = experiment.ResetVersion;
                ResetPose();
            }
            if (experiment.IsReviewing && IsHeld) ResetPose();
            UpdateTarget();
            experiment.SetSprayInput(trigger && IsHeld, IsOnTarget);
            bool active = experiment.SprayActive && IsHeld;
            if (spray != null && active != wasSpraying)
            {
                if (active) spray.Play(true);
                else spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            wasSpraying = active;
            if (lever != null)
                lever.localRotation = leverRest * Quaternion.Euler(0f, 0f, trigger && IsHeld ? 13f : 0f);
            UpdateHose();
            RenderAim();
        }

        private void UpdateTarget()
        {
            IsOnTarget = false;
            if (!IsHeld || nozzle == null || experiment == null || experiment.IsReviewing) return;
            Vector3 target = WirePathGeometry.Point(experiment.Sample.FaultWire, experiment.Sample.FaultPosition)
                + Vector3.up * .035f;
            if (circuitFrame != null) target = circuitFrame.TransformPoint(target);
            Vector3 offset = target - NozzlePosition;
            float distance = offset.magnitude;
            IsOnTarget = distance >= .10f && distance <= SprayRange
                && Vector3.Dot(nozzle.forward, offset.normalized) >= Mathf.Cos(SprayHalfAngle * Mathf.Deg2Rad);
        }

        private void RenderAim()
        {
            ShowAim(IsHeld);
            if (!IsHeld || nozzle == null) return;
            Vector3 endpoint = NozzlePosition + nozzle.forward * Mathf.Clamp(Vector3.Distance(NozzlePosition, AimPoint), .1f, SprayRange);
            Color color = IsOnTarget ? new Color(.18f, 1f, .55f) : new Color(1f, .62f, .16f);
            if (aimGuide != null)
            {
                aimGuide.SetPosition(0, NozzlePosition);
                aimGuide.SetPosition(1, endpoint);
                aimGuide.startColor = aimGuide.endColor = color;
            }
            if (aimMarker != null)
            {
                aimMarker.transform.position = endpoint;
                if (markerProperties == null) markerProperties = new MaterialPropertyBlock();
                markerProperties.SetColor("_BaseColor", color);
                markerProperties.SetColor("_EmissionColor", color * .7f);
                aimMarker.SetPropertyBlock(markerProperties);
            }
        }

        private void ShowAim(bool visible)
        {
            if (aimGuide != null) aimGuide.enabled = visible;
            if (aimMarker != null) aimMarker.enabled = visible;
        }

        private void UpdateHose()
        {
            if (hose == null || hornPivot == null) return;
            Vector3 start = transform.TransformPoint(new Vector3(-.01f, .36f, 0f));
            Vector3 end = hornPivot.position;
            hose.SetPosition(0, start);
            hose.SetPosition(1, Vector3.Lerp(start, end, .3f) + transform.right * -.04f);
            hose.SetPosition(2, Vector3.Lerp(start, end, .68f) + Vector3.down * .08f);
            hose.SetPosition(3, end);
        }

        private void OnDisable()
        {
            Release();
            if (spray != null) spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            wasSpraying = false;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus) SetTrigger(false);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SetTrigger(false);
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
