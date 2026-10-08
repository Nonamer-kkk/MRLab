using UnityEngine;

namespace MRLab.Joule
{
    public enum LabAction
    {
        Inspect,
        Power,
        Fault,
        Isolate,
        Alarm,
        Extinguish
    }

    /// <summary>Platform-neutral action attached to a laboratory device.</summary>
    public sealed class LabInteractable : MonoBehaviour
    {
        public LabAction Action;
        public string Label;
    }
}
