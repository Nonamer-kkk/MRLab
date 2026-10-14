using UnityEngine;
using UnityEngine.EventSystems;

namespace MRLab.Joule
{
    /// <summary>A momentary switch: leaving or releasing always stops spraying.</summary>
    public sealed class JouleSprayHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public JoulePCInput Input;
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left && Input != null) Input.SetUiSpray(true); }
        public void OnPointerUp(PointerEventData data) { Stop(); }
        public void OnPointerExit(PointerEventData data) { Stop(); }
        private void OnDisable() { Stop(); }
        private void Stop() { if (Input != null) Input.SetUiSpray(false); }
    }
}
