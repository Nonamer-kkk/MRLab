using UnityEngine;
namespace MRLab.Joule
{
    public sealed class JouleResetRelay : MonoBehaviour
    {
        public JouleExperimentManager Experiment;
        public void ResetAll(){if(Experiment!=null)Experiment.ResetExperiment();}
    }
}
