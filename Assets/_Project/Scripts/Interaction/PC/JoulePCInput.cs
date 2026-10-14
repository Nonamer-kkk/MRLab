using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MRLab.Joule
{
    /// <summary>PC adapter for input-independent experiment and grab/aim/trigger commands.</summary>
    public sealed class JoulePCInput : MonoBehaviour
    {
        [SerializeField] private JouleExperimentManager experiment;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private float orbitSensitivity = .18f, zoomSensitivity = .7f;
        private ExtinguisherInteraction extinguisher;
        private Transform circuitFrame;
        private InputAction pointerPress;
        private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
        private readonly Vector3 orbitPivot = new Vector3(.20f, 1.0f, -.05f);
        private float yaw = 20, pitch = 38, distance = 3.8f;
        private bool dragging, uiTrigger;
        private bool focused = true, requireRelease;
        private bool pendingPointerDown, scenePointerHeld, modalBlocked;
        private Vector2 pointerDownPosition;
        private Plane dragPlane;
        private Vector3 dragOffset;
        private int resetVersion;
        public JouleExperimentManager Experiment => experiment;
        public bool SelectingFault { get; private set; }
        public bool HoldingExtinguisher => extinguisher != null && extinguisher.IsHeld;
        public bool DraggingExtinguisher => dragging;
        public bool AimingAtFire => HoldingExtinguisher && extinguisher.IsOnTarget;
        public bool ModalBlocked
        {
            get => modalBlocked;
            set
            {
                if (modalBlocked == value) return;
                modalBlocked = value;
                SuspendPointerInput();
                requireRelease = true;
            }
        }
        public string InteractionHint => SelectingFault
            ? "选点：左键按住导线拖动，松开确认；Esc 取消"
            : HoldingExtinguisher
                ? (dragging ? "移动：拖动瓶身；滚轮调节远近；松手后瞄准" : "左拖空处瞄准；右键按住扫射；左拖瓶身移动；Esc 收回")
                : "左拖灭火器可取用；右拖环绕；滚轮缩放";

        public void Bind(JouleExperimentManager target, Camera camera)
        { experiment = target; sceneCamera = camera; FindCircuitFrame(); UpdateCamera(); }
        private void Awake()
        {
            if (experiment == null) experiment = FindObjectOfType<JouleExperimentManager>();
            if (sceneCamera == null) sceneCamera = Camera.main;
            extinguisher = FindObjectOfType<ExtinguisherInteraction>();
            FindCircuitFrame();
            resetVersion = experiment == null ? 0 : experiment.ResetVersion;
        }
        private void OnEnable()
        {
            pointerPress = new InputAction("LaboratoryPointerPress", InputActionType.Button, "<Mouse>/leftButton");
            pointerPress.performed += CapturePointerDown;
            pointerPress.Enable();
        }
        private void CapturePointerDown(InputAction.CallbackContext context)
        {
            if (!focused || requireRelease || modalBlocked || pendingPointerDown) return;
            var mouse = context.control.device as Mouse;
            if (mouse == null) return;
            Vector2 point = mouse.position.ReadValue();
            if (!Finite(point)) return;
            // Input events can press, move and release before the next render frame.
            // Pick from the true press position, then apply the final pose in Update.
            pointerDownPosition = point;
            pendingPointerDown = true;
        }
        private void Update()
        {
            if (experiment == null || sceneCamera == null) return;
            if (!focused || modalBlocked) return;
            if (requireRelease)
            {
                if (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed)) return;
                requireRelease = false;
            }
            if (resetVersion != experiment.ResetVersion || experiment.IsReviewing)
            { CancelInteraction(); resetVersion = experiment.ResetVersion; }
            HandleKeyboard();
            if (!experiment.IsReviewing) HandleMouse();
            HandleCamera();
        }
        public void ToggleFaultSelection()
        {
            if (modalBlocked || !experiment.CanSelectFaultLocation) return;
            bool selecting = !SelectingFault;
            CancelInteraction(); SelectingFault = selecting;
            experiment.SetInteractionMessage(selecting
                ? "在任意实验导线上按住左键拖动故障点，松开确认。通电后位置锁定。"
                : "已退出选点，保留当前故障位置。");
        }
        public void ToggleExtinguisher()
        {
            if (modalBlocked || experiment.IsReviewing || extinguisher == null) return;
            if (HoldingExtinguisher) { CancelInteraction(); return; }
            SelectingFault = false;
            extinguisher.Grab();
            extinguisher.MoveTo(LabToWorld(new Vector3(.85f, 1.35f, -.65f)));
            experiment.SetInteractionMessage("左拖瓶身移动，左拖空处瞄准火焰根部。按住右键扫射，或瞄准后按住喷射开关。");
        }
        public void SetUiSpray(bool pressed)
        {
            uiTrigger = pressed && focused && !requireRelease && !modalBlocked && HoldingExtinguisher && !experiment.IsReviewing;
            if (!uiTrigger && extinguisher != null) extinguisher.SetTrigger(false);
        }
        public void SuspendPointerInput()
        { dragging = scenePointerHeld = pendingPointerDown = false; SetUiSpray(false); if (extinguisher != null) extinguisher.SetTrigger(false); }
        private void HandleKeyboard()
        {
            var keys = Keyboard.current; if (keys == null) return;
            if (keys.escapeKey.wasPressedThisFrame) CancelInteraction();
            if (keys.tKey.wasPressedThisFrame) experiment.ToggleThermalOverlay();
            if (keys.rKey.wasPressedThisFrame) { CancelInteraction(); experiment.ResetExperiment(); return; }
            if (experiment.IsReviewing)
            { if (keys.spaceKey.wasPressedThisFrame) experiment.ToggleReplay(); return; }
            if (keys.pKey.wasPressedThisFrame) experiment.TogglePower();
            if (keys.fKey.wasPressedThisFrame) experiment.TriggerFault(FaultMode.ShortCircuit);
            if (keys.gKey.wasPressedThisFrame) experiment.TriggerFault(FaultMode.PoorContact);
            if (keys.eKey.wasPressedThisFrame) experiment.IsolatePower();
            if (keys.aKey.wasPressedThisFrame) experiment.RaiseAlarm();
            if (keys.xKey.wasPressedThisFrame) ToggleExtinguisher();
            if (keys.spaceKey.wasPressedThisFrame) experiment.SimulationSpeed = experiment.SimulationSpeed > 0 ? 0 : 1;
        }
        private bool OverViewport(Vector2 point)
        {
            if (sceneCamera == null || !Finite(point) || !sceneCamera.pixelRect.Contains(point)) return false;
            var system = EventSystem.current;
            if (system == null) return true;
            pointerHits.Clear();
            system.RaycastAll(new PointerEventData(system) { position = point }, pointerHits);
            for (int i = 0; i < pointerHits.Count; i++)
                if (pointerHits[i].module is UnityEngine.UI.GraphicRaycaster) return false;
            return true;
        }
        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) { SuspendPointerInput(); return; }
            Vector2 point = mouse.position.ReadValue();
            if (!Finite(point)) { SuspendPointerInput(); return; }
            bool over = OverViewport(point);
            Ray ray = sceneCamera.ScreenPointToRay(point);
            bool pressed = pendingPointerDown;
            Vector2 pressedAt = pointerDownPosition;
            pendingPointerDown = false;
            bool pressedOver = pressed && OverViewport(pressedAt);
            if (pressed) scenePointerHeld = pressedOver;
            bool released = scenePointerHeld && !mouse.leftButton.isPressed;
            if (!experiment.CanSelectFaultLocation) SelectingFault = false;
            if (SelectingFault)
            {
                if (over && scenePointerHeld)
                {
                    if (TryPickWire(point, out int wire, out float position)) experiment.SetFaultLocation(wire, position);
                    else experiment.SetInteractionMessage("此处没有实验导线。请把鼠标移到可见导线上，故障点不会放在空处。");
                }
                if (released) { SelectingFault = false; scenePointerHeld = false; }
                return;
            }
            Ray pressRay = pressedOver ? sceneCamera.ScreenPointToRay(pressedAt) : ray;
            if (pressedOver && Physics.Raycast(pressRay, out RaycastHit hit, 100))
            {
                var target = hit.collider.GetComponentInParent<LabInteractable>();
                if (target != null)
                {
                    if (target.Action == LabAction.Extinguish && extinguisher != null)
                    {
                        extinguisher.Grab(); dragging = true;
                        dragPlane = new Plane(-sceneCamera.transform.forward, extinguisher.transform.position);
                        if (dragPlane.Raycast(pressRay, out float enter)) dragOffset = extinguisher.transform.position - pressRay.GetPoint(enter);
                    }
                    else if (!HoldingExtinguisher)
                    {
                        switch (target.Action)
                        {
                            case LabAction.Inspect: experiment.Inspect(); break;
                            case LabAction.Power: experiment.TogglePower(); break;
                            case LabAction.Fault: experiment.TriggerFault(FaultMode.ShortCircuit); break;
                            case LabAction.Isolate: experiment.IsolatePower(); break;
                            case LabAction.Alarm: experiment.RaiseAlarm(); break;
                        }
                    }
                }
            }
            bool movedBody = dragging;
            if (dragging)
            {
                if (over && scenePointerHeld && dragPlane.Raycast(ray, out float enter))
                {
                    dragOffset += sceneCamera.transform.forward * (mouse.scroll.ReadValue().y / 120f) * .12f;
                    Vector3 pose = ray.GetPoint(enter) + dragOffset;
                    if (circuitFrame != null) pose = circuitFrame.InverseTransformPoint(pose);
                    pose.x = Mathf.Clamp(pose.x, -2.1f, 2.1f);
                    pose.y = Mathf.Clamp(pose.y, .43f, 2.5f);
                    pose.z = Mathf.Clamp(pose.z, -1.5f, 1.1f);
                    extinguisher.MoveTo(LabToWorld(pose));
                }
                if (released || !over) dragging = false;
            }
            if (!HoldingExtinguisher) { if (released) scenePointerHeld = false; return; }
            if (over && !movedBody && (scenePointerHeld || mouse.rightButton.isPressed))
            {
                // The table-height surface permits aiming anywhere, including deliberate misses.
                Plane aimSurface = new Plane(circuitFrame != null ? circuitFrame.up : Vector3.up, LabToWorld(new Vector3(0, .95f, 0)));
                if (aimSurface.Raycast(ray, out float aimDistance)) extinguisher.AimAt(ray.GetPoint(aimDistance));
            }
            extinguisher.SetTrigger(!dragging && (uiTrigger || (over && mouse.rightButton.isPressed)));
            if (released) scenePointerHeld = false;
        }
        private bool TryPickWire(Vector2 screen, out int wire, out float position)
        {
            // Screen-space picking keeps thin wires usable; saved positions use continuous arc length.
            float best = 18f * 18f; wire = -1; position = 0;
            for (int w = 0; w < 4; w++)
            {
                Vector3 previous = sceneCamera.WorldToScreenPoint(LabToWorld(WirePathGeometry.Point(w, 0)));
                for (int i = 1; i <= 100; i++)
                {
                    Vector3 next = sceneCamera.WorldToScreenPoint(LabToWorld(WirePathGeometry.Point(w, i / 100f)));
                    Vector2 start = previous, delta = (Vector2)next - start;
                    float t = delta.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(screen - start, delta) / delta.sqrMagnitude) : 0;
                    float squared = (screen - (start + delta * t)).sqrMagnitude;
                    if (previous.z > 0 && next.z > 0 && squared < best)
                    { best = squared; wire = w; position = (i - 1 + t) / 100f; }
                    previous = next;
                }
            }
            return wire >= 0;
        }
        private void HandleCamera()
        {
            if (Mouse.current == null) return;
            bool over = OverViewport(Mouse.current.position.ReadValue());
            if (!HoldingExtinguisher && !SelectingFault && Mouse.current.rightButton.isPressed && over)
            {
                yaw += Mouse.current.delta.ReadValue().x * orbitSensitivity;
                pitch = Mathf.Clamp(pitch - Mouse.current.delta.ReadValue().y * orbitSensitivity, 8, 65); UpdateCamera();
            }
            float wheel = Mouse.current.scroll.ReadValue().y / 120f;
            if (!dragging && Mathf.Abs(wheel) > .001f && over)
            { distance = Mathf.Clamp(distance - wheel * zoomSensitivity, 3.8f, 10); UpdateCamera(); }
            if (Keyboard.current != null && Keyboard.current.homeKey.wasPressedThisFrame)
            { yaw = 20; pitch = 38; distance = 3.8f; UpdateCamera(); }
        }
        private void UpdateCamera()
        {
            if (sceneCamera == null) return;
            sceneCamera.transform.position = LabToWorld(orbitPivot + Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -distance));
            sceneCamera.transform.LookAt(LabToWorld(orbitPivot), circuitFrame != null ? circuitFrame.up : Vector3.up);
        }
        private void FindCircuitFrame()
        {
            var visuals = FindObjectOfType<LabVisualController>();
            circuitFrame = visuals != null ? visuals.transform : null;
        }
        private Vector3 LabToWorld(Vector3 point) => circuitFrame != null ? circuitFrame.TransformPoint(point) : point;
        private static bool Finite(Vector2 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x)
            && !float.IsNaN(point.y) && !float.IsInfinity(point.y);
        private void CancelInteraction()
        {
            SelectingFault = dragging = uiTrigger = scenePointerHeld = pendingPointerDown = false;
            if (extinguisher != null) { extinguisher.SetTrigger(false); extinguisher.ResetPose(); }
        }
        private void OnApplicationFocus(bool hasFocus)
        { focused = hasFocus; if (!hasFocus) { SuspendPointerInput(); requireRelease = true; } }
        private void OnApplicationPause(bool paused)
        { if (paused) { SuspendPointerInput(); requireRelease = true; } }
        private void OnDisable()
        {
            CancelInteraction();
            if (pointerPress == null) return;
            pointerPress.performed -= CapturePointerDown;
            pointerPress.Disable();
            pointerPress.Dispose();
            pointerPress = null;
        }
    }
}
