using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MRLab.Joule
{
    /// <summary>PC presentation: TMP only. Every action is sent to the shared experiment manager.</summary>
    public sealed class JouleLabUI : MonoBehaviour
    {
        public TMP_FontAsset Font;
        public JouleExperimentManager Experiment;
        private static readonly Color Ink=new Color(.045f,.075f,.105f);
        private static readonly Color Surface=new Color(.071f,.11f,.145f,.985f);
        private static readonly Color Raised=new Color(.112f,.17f,.20f);
        private static readonly Color Muted=new Color(.58f,.69f,.74f);
        private static readonly Color Teal=new Color(.28f,.91f,.77f);
        private static readonly Color Amber=new Color(1f,.67f,.28f);
        private static readonly Color White=new Color(.91f,.95f,.95f);
        private TMP_Text current,temperature,power,energy,phase,message,stage,events,time,formula;
        private TMP_Text protectionLabel,overlayLabel,reviewLabel,speedLabel,axisI,axisT,axisTimeI,axisTimeT;
        private TMP_Text gaugeValue,loadValue,contactValue,positionValue,interactionHint,sprayStatus;
        private TMP_Text locationButtonLabel,extinguisherButtonLabel;
        private JoulePCInput pcInput;
        private Button sprayButton;
        private Button powerButton,reviewButton,speedButton;
        private readonly List<Button> parameterButtons=new List<Button>();
        private readonly List<Button> liveButtons=new List<Button>();
        private readonly Image[] stepBars=new Image[5];
        private Slider replay;
        private JouleChartGraphic currentChart,temperatureChart;
        private GameObject help;
        private float nextRefresh;
        private Camera sceneCamera;
        private RectTransform contentRoot;
        private readonly RectTransform[] letterboxBars=new RectTransform[4];
        private int screenWidth,screenHeight;
        private readonly float[] areas={.5f,1.5f,2.5f};
        private readonly float[] loads={6,12,24};
        private readonly float[] contacts={.08f,.4f,1.2f};

        private void Start()
        {
            if(Experiment==null)Experiment=FindObjectOfType<JouleExperimentManager>();
            pcInput=FindObjectOfType<JoulePCInput>();
            if(Font==null)Font=TMP_Settings.defaultFontAsset;
            CreateUI(); Refresh();
        }
        private void Update()
        {
            UpdateLayout();
            if(Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+.08f;Refresh();
        }
        private void CreateUI()
        {
            var canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);
            // Keep the authored 16:9 work surface wholly visible in narrow, tall, or ultrawide windows.
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            sceneCamera=Camera.main;
            for(int i=0;i<letterboxBars.Length;i++)
                letterboxBars[i]=Box(transform,"Letterbox"+i,0,0,0,0,Ink).rectTransform;
            contentRoot=Rect(transform,"Content",0,0,1600,900);
            contentRoot.anchorMin=contentRoot.anchorMax=new Vector2(.5f,.5f);
            contentRoot.pivot=new Vector2(.5f,.5f);contentRoot.anchoredPosition=Vector2.zero;
            Transform root=contentRoot;
            UpdateLayout();
            Box(root,"Header",0,0,1600,108,Ink);
            Box(root,"BrandAccent",24,27,4,44,Teal);
            Text(root,"E10   /   ELECTRICAL SAFETY LAB",40,20,500,20,13,Muted);
            Text(root,"焦耳定律 · 短路起火",40,45,630,40,30,White);
            Text(root,"实验室 01     /     PC 交互实验",1100,24,460,23,16,Muted,TextAlignmentOptions.Right);
            Button(root,"操作说明",1372,59,90,29,()=>SetHelpVisible(!help.activeSelf),Raised,13);
            Button(root,"退出",1476,59,84,29,()=>Application.Quit(),Raised,13);
            string[] steps={"01  巡检","02  设参","03  触发","04  处置","05  复盘"};
            for(int i=0;i<5;i++)
            {
                Text(root,steps[i],688+i*128,48,125,25,16,Muted);
                stepBars[i]=Box(root,"Progress"+i,688+i*128,89,106,3,Muted*.4f);
            }

            var left=Box(root,"Parameters",20,126,256,548,Surface).transform;
            Text(left,"实验设置",20,17,216,28,21,White);
            Text(left,"24 V 教学电源 · 巡检后设参",20,51,226,20,12,Muted);
            Line(left,20,85,216);
            Text(left,"导线截面积",20,99,216,23,15,Muted);
            gaugeValue=Choice(left,132,()=>StepWire(-1),()=>StepWire(1));
            Text(left,"负载电阻",20,184,216,23,15,Muted);
            loadValue=Choice(left,217,()=>StepLoad(-1),()=>StepLoad(1));
            Text(left,"故障点接触电阻",20,268,218,23,15,Muted);
            contactValue=Choice(left,301,()=>StepContact(-1),()=>StepContact(1));
            Text(left,"故障位置 · 沿导线自由选择",20,347,216,23,14,Muted);
            positionValue=Text(left,"",20,373,216,25,14,Teal);
            var locationButton=Button(left,"在导线上选点 / 拖动",20,405,216,32,()=>pcInput?.ToggleFaultSelection(),Raised,14);
            parameterButtons.Add(locationButton);locationButtonLabel=locationButton.GetComponentInChildren<TMP_Text>();
            var pb=Button(left,"保护器  关闭",20,450,216,38,()=>{if(CanEditParameters())Experiment.SetProtection(!Experiment.Parameters.ProtectionEnabled);},Raised,16);
            parameterButtons.Add(pb);
            protectionLabel=pb.GetComponentInChildren<TMP_Text>();
            Text(left,"10 A 过流保护 · 接触过热可能不跳闸",20,500,218,30,11,Muted);

            var rigTitle=Box(root,"ObservationHeader",292,126,1000,83,Surface).transform;
            Metric(rigTitle,"回路电流",20,out current,Teal);
            Metric(rigTitle,"故障点温度",270,out temperature,Amber);
            Metric(rigTitle,"局部焦耳功率",520,out power,White);
            Metric(rigTitle,"累计焦耳热",770,out energy,White);
            var viewport=Box(root,"ViewportBadge",305,224,258,34,new Color(.04f,.08f,.11f,.87f)).transform;
            Text(viewport,"LIVE  /  三维实验台",12,5,234,24,14,Teal);
            phase=Text(root,"等待巡检",1100,225,173,28,15,Teal,TextAlignmentOptions.Right);
            interactionHint=Text(root,"",310,263,960,24,13,White);
            stage=Text(root,"完好",308,577,420,32,24,Teal);
            var overlay=Button(root,"热风险叠加：开  [T]",1082,578,194,32,()=>Experiment.ToggleThermalOverlay(),Raised,13);
            overlayLabel=overlay.GetComponentInChildren<TMP_Text>();
            var msg=Box(root,"ActionFeedback",292,625,1000,49,Surface).transform;
            message=Text(msg,"点击「巡检线路」开始实验。",16,7,968,35,16,White);

            var right=Box(root,"Actions",1308,126,272,548,Surface).transform;
            Text(right,"实验操作",18,16,234,30,21,White);
            Text(right,"顺序完成五步教学流程",18,52,234,21,12,Muted);
            LiveButton(right,"01   巡检线路与保护器",18,89,236,40,()=>Experiment.Inspect(),Raised,16);
            powerButton=LiveButton(right,"02   合上电源  [P]",18,143,236,40,()=>Experiment.TogglePower(),new Color(.08f,.33f,.29f),16);
            LiveButton(right,"03   触发短路  [F]",18,197,236,40,()=>Experiment.TriggerFault(FaultMode.ShortCircuit),new Color(.47f,.29f,.12f),16);
            LiveButton(right,"接触不良  [G]",18,247,236,32,()=>Experiment.TriggerFault(FaultMode.PoorContact),Raised,14);
            LiveButton(right,"04   紧急断电  [E]",18,306,236,43,()=>Experiment.IsolatePower(),new Color(.60f,.22f,.18f),16);
            LiveButton(right,"报警  [A]",18,361,111,37,()=>Experiment.RaiseAlarm(),Raised,15);
            var equip=LiveButton(right,"取用  [X]",141,361,113,37,()=>pcInput?.ToggleExtinguisher(),Raised,15);
            extinguisherButtonLabel=equip.GetComponentInChildren<TMP_Text>();
            sprayButton=LiveButton(right,"按住喷射",18,404,111,32,()=>{},new Color(.13f,.35f,.38f),14);
            sprayButton.gameObject.AddComponent<JouleSprayHoldButton>().Input=pcInput;
            sprayStatus=Text(right,"未取用",137,401,119,36,12,Teal);
            reviewButton=Button(right,"05   实验复盘",18,444,236,33,()=>{if(Experiment.IsReviewing)Experiment.EndReview();else Experiment.BeginReview();},Raised,16);
            reviewLabel=reviewButton.GetComponentInChildren<TMP_Text>();
            Button(right,"复位并保留对照  [R]",18,487,236,35,()=>Experiment.ResetExperiment(),new Color(.13f,.22f,.26f),14);

            var data=Box(root,"DataFooter",292,690,1288,190,Surface).transform;
            Text(data,"电流  /  A",18,9,234,23,15,Teal);
            Text(data,"温度  /  °C",492,9,234,23,15,Amber);
            Text(data,"实线：本次    虚线：上次",710,10,220,20,11,Muted,TextAlignmentOptions.Right);
            currentChart=Chart(data,48,42,402,103,false,Teal);
            temperatureChart=Chart(data,522,42,402,103,true,Amber);
            axisI=Text(data,"80",10,35,35,19,11,Muted,TextAlignmentOptions.Right);
            axisT=Text(data,"400",477,35,40,19,11,Muted,TextAlignmentOptions.Right);
            Text(data,"0",15,129,30,19,11,Muted,TextAlignmentOptions.Right);
            Text(data,"0",480,129,37,19,11,Muted,TextAlignmentOptions.Right);
            Text(data,"0 s",46,149,50,20,11,Muted);
            Text(data,"0 s",521,149,50,20,11,Muted);
            axisTimeI=Text(data,"30 s",393,149,59,20,11,Muted,TextAlignmentOptions.Right);
            axisTimeT=Text(data,"30 s",868,149,59,20,11,Muted,TextAlignmentOptions.Right);
            Box(data,"Divider",948,17,1,152,new Color(.2f,.31f,.36f));
            time=Text(data,"00:00.0",972,12,154,35,25,White);
            speedButton=LiveButton(data,"1×",1170,14,96,29,()=>{float s=Experiment.SimulationSpeed;Experiment.SimulationSpeed=s==1?3:s==3?0:1;},Raised,13);
            speedLabel=speedButton.GetComponentInChildren<TMP_Text>();
            Text(data,"回放时间轴",972,58,210,22,13,Muted);
            replay=Slider(data,972,94,290,value=>{if(Experiment.IsReviewing)Experiment.SetReplayNormalized(value);});
            Button(data,"播放 / 暂停",972,132,137,34,()=>{if(!Experiment.IsReviewing)Experiment.BeginReview();else Experiment.ToggleReplay();},Raised,13);
            Button(data,"导出 CSV",1121,132,145,34,()=>{string path=Experiment.ExportCsv();Debug.Log("CSV_EXPORT: "+path);},Raised,13);

            var note=Box(root,"Learning",20,690,256,190,Surface).transform;
            Text(note,"Q = ∫ I²R dt",17,12,226,31,23,White);
            formula=Text(note,"短路旁路负载 → 电流增大\n焦耳热积累 → 局部温升",17,53,222,49,13,Muted);
            events=Text(note,"等待记录实验事件",17,109,222,64,11,Teal);
            CreateHelp(root);
        }

        private void Refresh()
        {
            if(Experiment==null || current==null)return;
            foreach(Button control in liveButtons)control.interactable=!Experiment.IsReviewing;
            bool editing=CanEditParameters();
            foreach(Button control in parameterButtons)control.interactable=editing;
            var s=Experiment.DisplaySample;
            current.text=s.Current.ToString("0.00")+" <size=15>A</size>";
            temperature.text=s.Temperature.ToString("0.0")+" <size=15>°C</size>";
            power.text=s.HeatingPower.ToString("0.0")+" <size=15>W</size>";
            energy.text=(s.HeatEnergy/1000f).ToString("0.00")+" <size=15>kJ</size>";
            gaugeValue.text=Experiment.Parameters.WireArea.ToString("0.0")+" mm²";
            loadValue.text=Experiment.Parameters.LoadResistance.ToString("0")+" Ω";
            contactValue.text=Experiment.Parameters.ContactResistance.ToString("0.00")+" Ω";
            positionValue.text=WirePathGeometry.Name(s.FaultWire)+"  "+(s.FaultPosition*100).ToString("0.0")+"%";
            locationButtonLabel.text=pcInput!=null&&pcInput.SelectingFault?"正在选点 · 再按退出":"在导线上选点 / 拖动";
            bool equipped=pcInput!=null&&pcInput.HoldingExtinguisher;
            extinguisherButtonLabel.text=equipped?"收回  [X]":"取用  [X]";
            sprayButton.interactable=equipped&&!Experiment.IsReviewing;
            sprayStatus.text=!equipped?"未取用":Experiment.ExtinguishProgress>=1?"已扑灭":
                (Experiment.SprayActive?(Experiment.SprayOnTarget?"命中 ":"偏离 "):(pcInput.AimingAtFire?"已对准 ":"偏离 "))+(Experiment.ExtinguishProgress*100).ToString("0")+"%";
            interactionHint.text=Experiment.IsReviewing?"复盘中 · 场景交互已锁定":pcInput==null?"":pcInput.InteractionHint;
            protectionLabel.text=Experiment.Parameters.ProtectionEnabled?"保护器：启用  10 A":"保护器：关闭";
            protectionLabel.color=Experiment.Parameters.ProtectionEnabled?Teal:Amber;
            overlayLabel.text="热风险叠加："+(Experiment.ThermalOverlay?"开":"关")+"  [T]";
            reviewLabel.text=Experiment.IsReviewing?"退出复盘":"05   实验复盘";
            powerButton.GetComponentInChildren<TMP_Text>().text=Experiment.PowerOn?"02   断开电源  [P]":"02   合上电源  [P]";
            message.text=Experiment.LastMessage;
            phase.text=Experiment.IsReviewing?"REC  /  事故回放":s.BreakerTripped?"保护器已跳闸":s.Powered?"● 线路通电":"○ 线路断电";
            stage.text=StageName(s.Stage)+(s.Fault==FaultMode.None?"   /   正常回路":s.Fault==FaultMode.ShortCircuit?"   /   短路支路":"   /   接触不良");
            stage.color=s.Stage>=MaterialStage.Pyrolysis?Amber:Teal;
            time.text=s.Time.ToString("000.0")+" <size=15>s</size>";
            speedLabel.text=Experiment.IsReviewing?(Experiment.ReplayPlaying?"回放中":"已暂停"):Experiment.SimulationSpeed==0?"暂停":Experiment.SimulationSpeed.ToString("0")+"×";
            if(Experiment.IsReviewing)replay.SetValueWithoutNotify(Experiment.History.Count<2?0:(float)Experiment.ReplayIndex/(Experiment.History.Count-1));
            replay.interactable=Experiment.IsReviewing;
            StringBuilder log=new StringBuilder();
            for(int i=Mathf.Max(0,Experiment.Events.Count-3);i<Experiment.Events.Count;i++){if(log.Length>0)log.Append('\n');log.Append(Experiment.Events[i]);}
            events.text=log.ToString();
            int active=Experiment.IsReviewing?4:Experiment.Phase==ExperimentPhase.Completed||Experiment.Phase==ExperimentPhase.Isolated?3:s.Fault!=FaultMode.None?2:Experiment.IsInspected?1:0;
            for(int i=0;i<5;i++)stepBars[i].color=i<=active?Teal:new Color(.17f,.25f,.30f);
            currentChart.Refresh();temperatureChart.Refresh();
            axisI.text=currentChart.Maximum.ToString("0");axisT.text=temperatureChart.Maximum.ToString("0");
            axisTimeI.text=currentChart.TimeMaximum.ToString("0")+" s";axisTimeT.text=temperatureChart.TimeMaximum.ToString("0")+" s";
            formula.text=s.Fault==FaultMode.PoorContact?"接触电阻发热集中在端子\n过流保护可能无法检测局部过热":"短路旁路负载 → 电流增大\n焦耳热积累 → 局部温升";
        }
        public static string StageName(MaterialStage s)
        {
            switch(s){case MaterialStage.Normal:return "完好";case MaterialStage.Warming:return "升温 · 变色";case MaterialStage.Softened:return "绝缘层软化";case MaterialStage.Pyrolysis:return "热解 · 冒烟";case MaterialStage.Burning:return "起火";case MaterialStage.Charred:return "碳化 / 熄灭";default:return s.ToString();}
        }
        private void CreateHelp(Transform root)
        {
            help=Box(root,"HelpOverlay",0,0,1600,900,new Color(0,0,0,.70f)).gameObject;
            var sheet=Box(help.transform,"HelpSheet",380,174,840,556,Surface).transform;
            Text(sheet,"实验说明",32,25,775,42,28,White);
            Text(sheet,"在实验室中观察电流、温升与材料状态之间的关系。",32,79,775,35,18,Teal);
            Text(sheet,"1. 巡检并设参；按「选点」，沿任意实验导线左拖，松手确认。\n2. 合上电源再触发故障。火点锁定，观察温升和材料变化。\n3. 先断电、报警。左拖灭火器瓶身移动；拖动中滚轮调节远近。\n4. 左拖空处瞄准根部，右键按住扫射；也可瞄准后按住喷射开关。\n5. 命中累计 3 秒才扑灭；松手或偏离暂停进度。最后复盘。",32,136,775,181,18,White);
            Text(sheet,"未取用时右拖环绕、滚轮缩放；Home 恢复视角；Esc 收回\nP 通断电  F 短路  G 接触不良  E 断电  A 报警  X 取用/收回\nT 热风险叠加  R 复位  Space 暂停/继续；暂停时不累计灭火",32,329,775,88,15,Muted);
            Text(sheet,"教学模型：24 V 集总电路与简化散热。温度和着火阈值用于演示趋势，\n不代表真实设备的安全限值。所有故障与火焰均为虚拟仿真。",32,429,775,58,14,Amber);
            Button(sheet,"返回实验",611,497,194,37,()=>SetHelpVisible(false),new Color(.10f,.35f,.29f),16);
            help.SetActive(false);
        }
        private void SetHelpVisible(bool visible)
        {
            if(pcInput!=null)pcInput.ModalBlocked=visible;
            help.SetActive(visible);
        }
        private TMP_Text Choice(Transform parent,float y,Action previous,Action next)
        {
            parameterButtons.Add(Button(parent,"−",20,y,36,36,previous,Raised,22));
            parameterButtons.Add(Button(parent,"+",200,y,36,36,next,Raised,22));
            return Text(parent,"",62,y,131,36,19,White,TextAlignmentOptions.Center);
        }
        private bool CanEditParameters()
        {
            return Experiment!=null && !Experiment.PowerOn && !Experiment.IsReviewing &&
                (Experiment.Phase==ExperimentPhase.Inspection || Experiment.Phase==ExperimentPhase.Ready);
        }
        private void StepWire(int direction)
        {
            if(CanEditParameters())Experiment.SetWireArea(NextValue(areas,Experiment.Parameters.WireArea,direction));
        }
        private void StepLoad(int direction)
        {
            if(CanEditParameters())Experiment.SetLoadResistance(NextValue(loads,Experiment.Parameters.LoadResistance,direction));
        }
        private void StepContact(int direction)
        {
            if(CanEditParameters())Experiment.SetContactResistance(NextValue(contacts,Experiment.Parameters.ContactResistance,direction));
        }
        private static float NextValue(float[] choices,float value,int direction)
        {
            int nearest=0;
            for(int i=1;i<choices.Length;i++)
                if(Mathf.Abs(choices[i]-value)<Mathf.Abs(choices[nearest]-value))nearest=i;
            return choices[(nearest+direction+choices.Length)%choices.Length];
        }
        private void UpdateLayout()
        {
            if(contentRoot==null || (screenWidth==Screen.width && screenHeight==Screen.height))return;
            screenWidth=Mathf.Max(1,Screen.width);screenHeight=Mathf.Max(1,Screen.height);
            float scale=Mathf.Min(screenWidth/1600f,screenHeight/900f);
            float canvasWidth=screenWidth/scale,canvasHeight=screenHeight/scale;
            float left=(canvasWidth-1600f)*.5f,top=(canvasHeight-900f)*.5f;
            SetRect(letterboxBars[0],0,0,canvasWidth,top);
            SetRect(letterboxBars[1],0,canvasHeight-top,canvasWidth,top);
            SetRect(letterboxBars[2],0,top,left,900);
            SetRect(letterboxBars[3],canvasWidth-left,top,left,900);
            if(sceneCamera==null)sceneCamera=Camera.main;
            if(sceneCamera!=null)
            {
                // Same 1000 x 400 observation area as the authored 1600 x 900 UI.
                float pixelLeft=(screenWidth-1600f*scale)*.5f;
                float pixelBottom=(screenHeight-900f*scale)*.5f;
                sceneCamera.rect=new UnityEngine.Rect((pixelLeft+292f*scale)/screenWidth,
                    (pixelBottom+286f*scale)/screenHeight,1000f*scale/screenWidth,400f*scale/screenHeight);
            }
        }
        private static void SetRect(RectTransform rect,float x,float y,float width,float height)
        {
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(Mathf.Max(0,width),Mathf.Max(0,height));
        }
        private Button LiveButton(Transform parent,string title,float x,float y,float w,float h,Action action,Color color,int size)
        {
            Button control=Button(parent,title,x,y,w,h,()=>{if(Experiment!=null&&!Experiment.IsReviewing)action();},color,size);
            liveButtons.Add(control);return control;
        }
        private void Metric(Transform parent,string title,float x,out TMP_Text number,Color color)
        {
            Text(parent,title,x,10,225,24,13,Muted);
            number=Text(parent,"0.00",x,32,225,43,30,color);
        }
        private JouleChartGraphic Chart(Transform parent,float x,float y,float w,float h,bool temperatureMode,Color color)
        {
            var rect=Rect(parent,"Chart",x,y,w,h);var chart=rect.gameObject.AddComponent<JouleChartGraphic>();
            chart.Experiment=Experiment;chart.Temperature=temperatureMode;chart.TraceColor=color;chart.raycastTarget=false;return chart;
        }
        private Slider Slider(Transform parent,float x,float y,float width,Action<float> action)
        {
            var r=Rect(parent,"ReplaySlider",x,y,width,20);var slider=r.gameObject.AddComponent<Slider>();
            var hitArea=r.gameObject.AddComponent<Image>();hitArea.color=Color.clear;
            Box(r,"Track",0,8,width,4,new Color(.22f,.34f,.40f));
            var handleArea=Rect(r,"HandleArea",6,0,width-12,20);
            var handle=Box(handleArea,"Handle",0,2,12,16,Teal);
            handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=new Vector2(0,.5f);
            handle.rectTransform.pivot=new Vector2(.5f,.5f);
            handle.rectTransform.anchoredPosition=Vector2.zero;
            slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.minValue=0;slider.maxValue=1;
            slider.navigation=new Navigation{mode=Navigation.Mode.None};
            slider.onValueChanged.AddListener(v=>action(v));return slider;
        }
        private Button Button(Transform parent,string title,float x,float y,float w,float h,Action action,Color color,int fontSize)
        {
            var bg=Box(parent,"Button_"+title,x,y,w,h,color);var b=bg.gameObject.AddComponent<Button>();b.targetGraphic=bg;
            var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.2f,1.2f,1.2f);colors.pressedColor=new Color(.7f,1f,.9f);colors.disabledColor=new Color(.4f,.4f,.4f);b.colors=colors;
            b.onClick.AddListener(()=>action());b.navigation=new Navigation{mode=Navigation.Mode.None};
            Text(bg.transform,title,4,0,w-8,h,fontSize,White,TextAlignmentOptions.Center);return b;
        }
        private TMP_Text Text(Transform parent,string content,float x,float y,float w,float h,int size,Color color,TextAlignmentOptions align=TextAlignmentOptions.Left)
        {
            var r=Rect(parent,"Text",x,y,w,h);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=Font;text.text=content;text.fontSize=size;text.color=color;
            text.alignment=align;text.enableWordWrapping=true;text.raycastTarget=false;
            // The Noto CJK face has a 1.448 line-height ratio. Auto-size keeps
            // 21-30 px headings and readouts visible in their authored boxes
            // instead of Ellipsis hiding an entire line when h is a few pixels
            // shorter than the calculated line height.
            if(size>=18)
            {
                text.enableAutoSizing=true;
                text.fontSizeMax=size;
                text.fontSizeMin=Mathf.Max(10,size*.55f);
                text.overflowMode=TextOverflowModes.Overflow;
            }
            else
            {
                text.enableAutoSizing=false;
                text.overflowMode=TextOverflowModes.Ellipsis;
            }
            return text;
        }
        private Image Box(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var r=Rect(parent,name,x,y,w,h);var img=r.gameObject.AddComponent<Image>();img.color=color;return img;
        }
        private void Line(Transform parent,float x,float y,float width){Box(parent,"Line",x,y,width,1,new Color(.20f,.29f,.34f));}
        private RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;
            r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
    }
}
