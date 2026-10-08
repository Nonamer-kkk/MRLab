using UnityEngine;
namespace MRLab.Joule
{
    /// <summary>Synthesized low-volume alarm/switch feedback; no external sound dependency.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class JouleLabAudio : MonoBehaviour
    {
        private JouleExperimentManager experiment;
        private AudioSource speaker;
        private AudioClip click,alarm;
        private bool lastPower,lastAlarm;
        private float nextAlarm;
        private void Start()
        {
            experiment=FindObjectOfType<JouleExperimentManager>();speaker=GetComponent<AudioSource>();speaker.playOnAwake=false;speaker.volume=.12f;
            click=Tone(480,.06f);alarm=Tone(770,.20f);
        }
        private AudioClip Tone(float frequency,float duration)
        {
            int count=(int)(22050*duration);float[] data=new float[count];
            for(int i=0;i<count;i++){float fade=Mathf.Sin(Mathf.PI*i/count);data[i]=Mathf.Sin(2*Mathf.PI*frequency*i/22050)*fade*.3f;}
            var clip=AudioClip.Create("LabTone",count,1,22050,false);clip.SetData(data,0);return clip;
        }
        private void Update()
        {
            if(experiment==null||experiment.IsReviewing)return;
            if(experiment.PowerOn!=lastPower){speaker.PlayOneShot(click);lastPower=experiment.PowerOn;}
            bool active=experiment.AlarmRaised && experiment.Phase!=ExperimentPhase.Completed;
            if(active&&Time.unscaledTime>nextAlarm){speaker.PlayOneShot(alarm);nextAlarm=Time.unscaledTime+1.5f;}
            if(!active&&lastAlarm)speaker.Stop();lastAlarm=active;
        }
        private void OnDestroy(){if(click!=null)Destroy(click);if(alarm!=null)Destroy(alarm);}
    }
}
