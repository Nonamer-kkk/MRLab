using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using MRLab.Joule;

namespace MRLab.Joule
{
    /// <summary>PC input mapping. It only translates mouse/keyboard input into experiment events.</summary>
    public sealed class JoulePCInput : MonoBehaviour
    {
        [SerializeField] private JouleExperimentManager experiment;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private float orbitSensitivity = 0.18f;
        [SerializeField] private float zoomSensitivity = 0.7f;

        private Vector3 orbitPivot = new Vector3(0f, 1.05f, 0f);
        private float yaw = 35f;
        private float pitch = 22f;
        private float distance = 6.2f;

        public JouleExperimentManager Experiment => experiment;

        public void Bind(JouleExperimentManager target, Camera camera)
        {
            experiment = target;
            sceneCamera = camera;
            if (sceneCamera != null)
            {
                sceneCamera.transform.position = orbitPivot + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);
                sceneCamera.transform.LookAt(orbitPivot);
            }
        }

        private void Awake()
        {
            if (experiment == null) experiment = FindObjectOfType<JouleExperimentManager>();
            if (sceneCamera == null) sceneCamera = Camera.main;
        }

        private void Update()
        {
            if (experiment == null) return;
            HandleKeyboard();
            HandleMouseSelection();
            HandleCamera();
        }

        private void HandleKeyboard()
        {
            var keys = Keyboard.current;
            if (keys == null) return;
            // Review only accepts replay, camera, overlay and reset controls.
            if (keys.tKey.wasPressedThisFrame) experiment.ToggleThermalOverlay();
            if (keys.rKey.wasPressedThisFrame) { experiment.ResetExperiment(); return; }
            if (experiment.IsReviewing)
            {
                if (keys.spaceKey.wasPressedThisFrame) experiment.ToggleReplay();
                return;
            }
            if (keys.pKey.wasPressedThisFrame) experiment.TogglePower();
            if (keys.fKey.wasPressedThisFrame) experiment.TriggerFault(FaultMode.ShortCircuit);
            if (keys.gKey.wasPressedThisFrame) experiment.TriggerFault(FaultMode.PoorContact);
            if (keys.eKey.wasPressedThisFrame) experiment.IsolatePower();
            if (keys.aKey.wasPressedThisFrame) experiment.RaiseAlarm();
            if (keys.xKey.wasPressedThisFrame) experiment.Extinguish();
            if (keys.spaceKey.wasPressedThisFrame)
            {
                experiment.SimulationSpeed = experiment.SimulationSpeed > 0f ? 0f : 1f;
            }
        }

        private void HandleMouseSelection()
        {
            if (experiment.IsReviewing || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || sceneCamera == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (!sceneCamera.pixelRect.Contains(Mouse.current.position.ReadValue())) return;
            Ray ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f)) return;
            LabInteractable interactable = hit.collider.GetComponentInParent<LabInteractable>();
            if (interactable == null) return;
            switch (interactable.Action)
            {
                case LabAction.Inspect: experiment.Inspect(); break;
                case LabAction.Power: experiment.TogglePower(); break;
                case LabAction.Fault: experiment.TriggerFault(FaultMode.ShortCircuit); break;
                case LabAction.Isolate: experiment.IsolatePower(); break;
                case LabAction.Alarm: experiment.RaiseAlarm(); break;
                case LabAction.Extinguish: experiment.Extinguish(); break;
            }
        }

        private void HandleCamera()
        {
            if (sceneCamera == null || Mouse.current == null) return;
            bool overViewport = sceneCamera.pixelRect.Contains(Mouse.current.position.ReadValue()) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            if (Mouse.current.rightButton.isPressed && overViewport)
            {
                yaw += Mouse.current.delta.ReadValue().x * orbitSensitivity;
                pitch = Mathf.Clamp(pitch - Mouse.current.delta.ReadValue().y * orbitSensitivity, 8f, 48f);
                UpdateCamera();
            }

            float wheel = Mouse.current.scroll.ReadValue().y / 120f;
            if (Mathf.Abs(wheel) > 0.001f && overViewport)
            {
                distance = Mathf.Clamp(distance - wheel * zoomSensitivity, 3.8f, 10f);
                UpdateCamera();
            }

            if (Keyboard.current != null && Keyboard.current.homeKey.wasPressedThisFrame)
            {
                yaw = 35f;
                pitch = 22f;
                distance = 6.2f;
                UpdateCamera();
            }
        }

        private void UpdateCamera()
        {
            sceneCamera.transform.position = orbitPivot + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);
            sceneCamera.transform.LookAt(orbitPivot);
        }
    }
}
