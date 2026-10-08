using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MRLab.Joule
{
    /// <summary>Lightweight chart. Both axes are labelled by JouleLabUI; old and current runs share scales.</summary>
    public sealed class JouleChartGraphic : MaskableGraphic
    {
        public JouleExperimentManager Experiment;
        public bool Temperature;
        public float Maximum = 100f;
        public float TimeMaximum = 60f;
        public Color TraceColor = new Color(0.23f, 0.87f, 0.77f);
        public void Refresh() { UpdateScales(); SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float w = rectTransform.rect.width, h = rectTransform.rect.height;
            Vector2 origin = rectTransform.rect.min;
            Color grid = new Color(.28f,.40f,.46f,.30f);
            for (int n=0;n<=4;n++)
            {
                Segment(vh,origin+new Vector2(0,h*n/4),origin+new Vector2(w,h*n/4),grid,.65f);
                Segment(vh,origin+new Vector2(w*n/4,0),origin+new Vector2(w*n/4,h),grid,.65f);
            }
            if (Experiment == null) return;
            var samples = Experiment.History;
            var previous = Experiment.PreviousHistory;
            UpdateScales();
            Draw(vh,previous,w,h,origin,new Color(.57f,.65f,.70f,.55f),true);
            Draw(vh,samples,w,h,origin,TraceColor,false);
            if(Experiment.IsReviewing)
            {
                float x = Experiment.DisplaySample.Time/TimeMaximum*w;
                Segment(vh,origin+new Vector2(x,0),origin+new Vector2(x,h),Color.white,1.2f);
            }
        }
        private void UpdateScales()
        {
            Maximum=Temperature?400:80;TimeMaximum=30;
            if(Experiment==null)return;
            Scan(Experiment.History);Scan(Experiment.PreviousHistory);
            Maximum=Mathf.Ceil(Maximum/(Temperature?100:20))*(Temperature?100:20);
            TimeMaximum=Mathf.Ceil(TimeMaximum/15f)*15f;
        }
        private void Scan(IReadOnlyList<JouleSample> values)
        {
            for(int i=0;i<values.Count;i++)
                Maximum=Mathf.Max(Maximum,(Temperature?values[i].Temperature:values[i].Current)*1.05f);
            if(values.Count>0) TimeMaximum=Mathf.Max(TimeMaximum,values[values.Count-1].Time);
        }
        private void Draw(VertexHelper vh,IReadOnlyList<JouleSample> values,float w,float h,Vector2 origin,Color tint,bool dashed)
        {
            int stride=Mathf.Max(1,values.Count/700);
            for(int i=stride;i<values.Count;i+=stride)
            {
                if(dashed && (i/stride)%4<2) continue;
                var a=values[i-stride];var b=values[i];
                Segment(vh,origin+new Vector2(a.Time/TimeMaximum*w,(Temperature?a.Temperature:a.Current)/Maximum*h),
                    origin+new Vector2(b.Time/TimeMaximum*w,(Temperature?b.Temperature:b.Current)/Maximum*h),tint,dashed?1.2f:2.4f);
            }
        }
        private static void Segment(VertexHelper vh,Vector2 a,Vector2 b,Color tint,float width)
        {
            Vector2 delta=b-a;if(delta.sqrMagnitude<.000001f)return;
            Vector2 normal=new Vector2(-delta.y,delta.x).normalized*width*.5f;
            int n=vh.currentVertCount;
            vh.AddVert(a-normal,tint,Vector2.zero);vh.AddVert(a+normal,tint,Vector2.zero);
            vh.AddVert(b+normal,tint,Vector2.zero);vh.AddVert(b-normal,tint,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
