using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MRLab.Joule
{
    public enum ExperimentPhase
    {
        Inspection,
        Ready,
        Running,
        Incident,
        Isolated,
        Completed,
        Review
    }

    /// <summary>
    /// PC presentation controller. It owns the deterministic model and its
    /// replay buffer, while UI/input scripts call the small public command API.
    /// </summary>
    public sealed class JouleExperimentManager : MonoBehaviour
    {
        [Header("Experiment setup")]
        public JouleParameters Parameters = new JouleParameters();
        public JouleSample Sample;
        public JouleSample DisplaySample;
        public ExperimentPhase Phase = ExperimentPhase.Inspection;
        public bool IsInspected;
        public bool AlarmRaised;
        public bool PowerOn;
        public bool IsReviewing;
        public bool ReplayPlaying;
        public bool ThermalOverlay = true;
        [Min(0f)] public float SimulationSpeed = 1f;
        public int ReplayIndex;

        private readonly List<JouleSample> _history = new List<JouleSample>();
        private readonly List<JouleSample> _previousHistory = new List<JouleSample>();
        private readonly List<string> _events = new List<string>();
        private JouleModel _model;
        private float _fixedAccumulator;
        private float _replayCursor;
        private bool _hasIncident;
        private bool _extinguished;
        private ExperimentPhase _phaseBeforeReview;
        private int _sampleStepCounter;

        public IReadOnlyList<JouleSample> History => _history;
        public IReadOnlyList<JouleSample> PreviousHistory => _previousHistory;
        public IReadOnlyList<string> Events => _events;
        public string LastMessage { get; private set; } = "请先检查实验台。";

        public event Action Changed;

        private const float FixedStep = 0.02f;
        private const int HistoryLimit = 18000;

        private void Awake()
        {
            EnsureModel();
            ResetExperiment();
        }

        private void Update()
        {
            AdvanceFrame(Time.deltaTime);
        }

        private void AdvanceFrame(float elapsed)
        {
            EnsureModel();
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0f) return;
            if (IsReviewing)
            {
                UpdateReplay(elapsed);
                return;
            }

            bool simulating = PowerOn && (Phase == ExperimentPhase.Running || Phase == ExperimentPhase.Incident);
            bool cooling = !PowerOn && Sample.Temperature > JouleModel.AmbientTemperature + 0.001f
                && (Phase == ExperimentPhase.Isolated || Phase == ExperimentPhase.Incident || Phase == ExperimentPhase.Completed);
            if (simulating || cooling)
            {
                float speed = Mathf.Clamp(SimulationSpeed, 0f, 3f);
                if (speed <= 0f || float.IsNaN(speed))
                {
                    return;
                }

                _fixedAccumulator += elapsed * speed;
                while (_fixedAccumulator >= FixedStep)
                {
                    _fixedAccumulator -= FixedStep;
                    AdvanceSimulation(FixedStep);
                }
            }
            else
            {
                DisplaySample = Sample;
            }
        }

        public void Inspect()
        {
            if (Phase != ExperimentPhase.Inspection && Phase != ExperimentPhase.Ready)
            {
                SetMessage("本次实验已进入运行或复盘阶段，请先重置实验。");
                return;
            }

            IsInspected = true;
            Phase = ExperimentPhase.Ready;
            SetMessage("检查完成。确认参数后可以接通电源。");
            RecordEvent("巡检完成");
            NotifyChanged();
        }

        public void TogglePower()
        {
            EnsureModel();
            if (!CanOperateLive()) return;
            if (PowerOn)
            {
                IsolatePower();
                return;
            }

            if (!IsInspected)
            {
                SetMessage("请先检查实验台，再接通电源。");
                return;
            }

            if (Phase != ExperimentPhase.Ready)
            {
                SetMessage("当前阶段不能直接接通电源，请先重置实验。");
                return;
            }

            Parameters.Sanitize();
            _model.Reset(Parameters);
            PowerOn = true;
            _model.SetPower(true);
            Phase = ExperimentPhase.Running;
            _extinguished = false;
            Sample = _model.Sample;
            DisplaySample = Sample;
            CaptureSample();
            SetMessage("电源已接通。先观察正常电流，再注入故障。");
            RecordEvent("接通电源");
            NotifyChanged();
        }

        public void IsolatePower()
        {
            EnsureModel();
            if (!CanOperateLive()) return;
            if (!PowerOn && Phase != ExperimentPhase.Incident)
            {
                SetMessage(Phase == ExperimentPhase.Isolated || Phase == ExperimentPhase.Completed
                    ? "电源已经隔离，请按当前流程继续。"
                    : "尚未接通电源，请先完成巡检。");
                return;
            }

            PowerOn = false;
            _model.SetPower(false);
            if (_hasIncident || Phase == ExperimentPhase.Incident || Sample.Stage == MaterialStage.Burning)
            {
                Phase = ExperimentPhase.Isolated;
                SetMessage("电源已隔离，电流归零。下一步：报警。断电后余热或火焰仍可能存在。");
            }
            else
            {
                Phase = ExperimentPhase.Completed;
                SetMessage("电源已断开，实验已结束。");
            }

            Sample = _model.Sample;
            DisplaySample = Sample;
            CaptureSample();
            RecordEvent("隔离电源");
            NotifyChanged();
        }

        public void TriggerFault(FaultMode fault)
        {
            EnsureModel();
            if (!CanOperateLive()) return;
            if (!PowerOn || Phase != ExperimentPhase.Running)
            {
                SetMessage(Phase == ExperimentPhase.Incident
                    ? "本轮故障已注入，请观察并完成处置；更换故障请重置。"
                    : "请先巡检并接通电源，再注入故障。");
                return;
            }

            if (fault == FaultMode.None)
            {
                SetMessage("当前为正常回路，可观察正常工况数据。");
                return;
            }

            if (!Enum.IsDefined(typeof(FaultMode), fault)) return;
            _model.InjectFault(fault);
            _hasIncident = true;
            Phase = ExperimentPhase.Incident;
            AlarmRaised = false;
            Sample = _model.Sample;
            DisplaySample = Sample;
            CaptureSample();
            SetMessage(fault == FaultMode.ShortCircuit
                ? "短路已注入：负载被旁路，电流突增。处置顺序：断电 → 报警 → 灭火。"
                : "接触不良已注入：局部接触电阻增大形成热点。处置顺序：断电 → 报警 → 灭火。");
            RecordEvent(fault == FaultMode.ShortCircuit ? "注入短路故障" : "注入接触不良故障");
            NotifyChanged();
        }

        public void RaiseAlarm()
        {
            if (!CanOperateLive()) return;
            if (PowerOn)
            {
                SetMessage("本实验要求先隔离电源，再报警。请先执行断电。");
                return;
            }

            if (!_hasIncident && Phase != ExperimentPhase.Incident && Sample.Stage != MaterialStage.Burning)
            {
                SetMessage("当前没有需要报警的异常。");
                return;
            }

            if (AlarmRaised)
            {
                SetMessage("报警已完成。保持隔离，有火焰时再使用灭火器。");
                return;
            }

            AlarmRaised = true;
            if (_model.Burning)
            {
                SetMessage("报警已完成。已确认断电，可以使用灭火器。");
            }
            else
            {
                Phase = ExperimentPhase.Completed;
                SetMessage("报警已完成，当前无明火。保持隔离，记录并复盘温升数据。");
            }

            RecordEvent("报警完成");
            NotifyChanged();
        }

        public void Extinguish()
        {
            EnsureModel();
            if (!CanOperateLive()) return;
            if (PowerOn)
            {
                SetMessage("灭火前必须先隔离电源。");
                return;
            }

            if (!AlarmRaised)
            {
                SetMessage("请先触发报警，再执行灭火操作。");
                return;
            }

            if (!_model.Burning)
            {
                SetMessage(_extinguished ? "明火已扑灭，请保持隔离并进入复盘。" : "当前无明火，无需喷射灭火剂。可以进入复盘。");
                return;
            }

            _model.Extinguish();
            _extinguished = true;
            Phase = ExperimentPhase.Completed;
            Sample = _model.Sample;
            DisplaySample = Sample;
            SetMessage("已完成灭火处置。保持隔离，进入复盘查看温升曲线。");
            CaptureSample();
            RecordEvent("确认断电后扑灭明火");
            NotifyChanged();
        }

        public void ResetExperiment()
        {
            EnsureModel();
            if (_history.Count > 0)
            {
                _previousHistory.Clear();
                _previousHistory.AddRange(_history);
            }
            _history.Clear();
            _events.Clear();
            _fixedAccumulator = 0f;
            _replayCursor = 0f;
            _sampleStepCounter = 0;
            ReplayIndex = 0;
            IsInspected = false;
            AlarmRaised = false;
            PowerOn = false;
            IsReviewing = false;
            ReplayPlaying = false;
            SimulationSpeed = 1f;
            ThermalOverlay = true;
            _hasIncident = false;
            _extinguished = false;
            Phase = ExperimentPhase.Inspection;
            Parameters = (Parameters ?? new JouleParameters()).Clone();
            Parameters.Sanitize();
            _model.Reset(Parameters);
            Sample = _model.Sample;
            DisplaySample = Sample;
            SetMessage("实验已重置。参数已保留，可重新检查并运行。");
            RecordEvent("实验复位，保留所选参数");
            NotifyChanged();
        }

        public void BeginReview()
        {
            if (IsReviewing) return;
            if (PowerOn)
            {
                SetMessage("复盘前必须先隔离电源。");
                return;
            }

            if (Sample.Stage == MaterialStage.Burning || Phase == ExperimentPhase.Incident)
            {
                SetMessage("现场仍有明火或未隔离，请按断电、报警、灭火的顺序完成处置。");
                return;
            }

            if (_hasIncident && !AlarmRaised)
            {
                SetMessage("异常处置尚未完成，请先报警，再进入复盘。");
                return;
            }

            if (_history.Count == 0)
            {
                SetMessage("暂无实验记录，先运行一次实验。");
                return;
            }

            CaptureSample();
            _phaseBeforeReview = Phase;
            IsReviewing = true;
            ReplayPlaying = false;
            _replayCursor = _history[0].Time;
            ReplayIndex = 0;
            Phase = ExperimentPhase.Review;
            DisplaySample = _history[0];
            SetMessage("已进入复盘。可拖动时间轴查看温升和电流。");
            NotifyChanged();
        }

        public void EndReview()
        {
            if (!IsReviewing) return;
            IsReviewing = false;
            ReplayPlaying = false;
            DisplaySample = Sample;
            Phase = _phaseBeforeReview;
            SetMessage("已退出复盘。");
            NotifyChanged();
        }

        public void SetReplayNormalized(float normalized)
        {
            if (!IsReviewing || _history.Count == 0 || float.IsNaN(normalized))
            {
                return;
            }

            _replayCursor = Mathf.Lerp(_history[0].Time, _history[_history.Count - 1].Time, Mathf.Clamp01(normalized));
            ReplayIndex = FindReplayIndex(_replayCursor);
            DisplaySample = _history[ReplayIndex];
            NotifyChanged();
        }

        public void ToggleReplay()
        {
            if (!IsReviewing || _history.Count == 0)
            {
                SetMessage("请先进入复盘。");
                return;
            }

            ReplayPlaying = !ReplayPlaying;
            if (ReplayPlaying && ReplayIndex >= _history.Count - 1)
            {
                _replayCursor = _history[0].Time;
                ReplayIndex = 0;
                DisplaySample = _history[0];
            }
            SetMessage(ReplayPlaying ? "复盘播放中。" : "复盘已暂停。");
            NotifyChanged();
        }

        public void ToggleThermalOverlay()
        {
            ThermalOverlay = !ThermalOverlay;
            SetMessage(ThermalOverlay ? "已显示热效应叠加。" : "已隐藏热效应叠加。");
            NotifyChanged();
        }

        public void SetWireArea(float area)
        {
            if (!CanEditParameters()) return;
            Parameters.WireArea = area;
            Parameters.Sanitize();
            ApplyParameterChange();
            SetMessage("已更新导线截面积。");
            NotifyChanged();
        }

        public void SetLoadResistance(float resistance)
        {
            if (!CanEditParameters()) return;
            Parameters.LoadResistance = resistance;
            Parameters.Sanitize();
            ApplyParameterChange();
            SetMessage("已更新负载电阻。");
            NotifyChanged();
        }

        public void SetContactResistance(float resistance)
        {
            if (!CanEditParameters()) return;
            Parameters.ContactResistance = resistance;
            Parameters.Sanitize();
            ApplyParameterChange();
            SetMessage("已更新接触电阻。");
            NotifyChanged();
        }

        public void SetProtection(bool enabled)
        {
            if (!CanEditParameters()) return;
            Parameters.ProtectionEnabled = enabled;
            ApplyParameterChange();
            SetMessage(enabled ? "已启用过流保护。" : "已关闭过流保护。");
            NotifyChanged();
        }

        public void SetFaultPosition(int position)
        {
            if (!CanEditParameters()) return;
            Parameters.FaultPosition = Mathf.Clamp(position, 0, 2);
            ApplyParameterChange();
            SetMessage("已更新故障位置。");
            NotifyChanged();
        }

        public string ExportCsv()
        {
            if (_history.Count == 0)
            {
                SetMessage("暂无记录可导出。");
                return string.Empty;
            }

            string directory = Path.Combine(Application.persistentDataPath, "Results");
            string path = Path.GetFullPath(Path.Combine(directory, "JouleExperiment_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".csv"));
            var builder = new StringBuilder();
            builder.AppendLine("Time_s,Current_A,Temperature_C,ElectricalHeat_J,LocalHeatingPower_W,Stage,Powered,BreakerTripped,Fault,Voltage_V,WireArea_mm2,LoadResistance_ohm,ContactResistance_ohm,ProtectionEnabled,FaultPosition");
            for (int i = 0; i < _history.Count; i++)
            {
                JouleSample sample = _history[i];
                builder.AppendLine(string.Join(",", sample.Time.ToString("F3", CultureInfo.InvariantCulture), sample.Current.ToString("F3", CultureInfo.InvariantCulture), sample.Temperature.ToString("F3", CultureInfo.InvariantCulture), sample.HeatEnergy.ToString("F3", CultureInfo.InvariantCulture), sample.HeatingPower.ToString("F3", CultureInfo.InvariantCulture), sample.Stage, sample.Powered, sample.BreakerTripped, sample.Fault, Parameters.Voltage.ToString("F3", CultureInfo.InvariantCulture), Parameters.WireArea.ToString("F3", CultureInfo.InvariantCulture), Parameters.LoadResistance.ToString("F3", CultureInfo.InvariantCulture), Parameters.ContactResistance.ToString("F3", CultureInfo.InvariantCulture), Parameters.ProtectionEnabled, Parameters.FaultPosition));
            }

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                SetMessage("数据导出失败，请检查结果目录权限：" + exception.Message);
                return string.Empty;
            }
            SetMessage("实验数据已导出：" + path);
            RecordEvent("导出实验 CSV");
            NotifyChanged();
            return path;
        }

        private bool CanEditParameters()
        {
            EnsureModel();
            if (PowerOn || IsReviewing || (Phase != ExperimentPhase.Inspection && Phase != ExperimentPhase.Ready))
            {
                SetMessage("运行中不能修改参数，请先复位实验。");
                return false;
            }

            return true;
        }

        private void AdvanceSimulation(float dt)
        {
            JouleSample previous = Sample;
            _model.Step(dt);
            Sample = _model.Sample;
            _sampleStepCounter++;
            if (_sampleStepCounter >= 5)
            {
                _sampleStepCounter = 0;
                CaptureSample();
            }

            DisplaySample = Sample;
            if (_model.Burning && previous.Stage != MaterialStage.Burning)
            {
                _hasIncident = true;
                Phase = PowerOn ? ExperimentPhase.Incident : ExperimentPhase.Isolated;
                SetMessage(PowerOn ? "材料已起火！请先隔离电源，再报警和灭火。" : "余热引发明火。电源已隔离，请报警并灭火。");
                RecordEvent("材料达到燃烧状态");
                CaptureSample();
            }

            if (Sample.BreakerTripped && !previous.BreakerTripped)
            {
                PowerOn = false;
                _hasIncident = true;
                Phase = ExperimentPhase.Isolated;
                SetMessage("10 A 过流保护已跳闸，电源自动隔离。请报警并复盘保护效果。");
                RecordEvent("过流保护跳闸");
                CaptureSample();
            }

            if (previous.Stage != Sample.Stage && Sample.Stage != MaterialStage.Burning)
            {
                RecordEvent("材料状态：" + StageName(Sample.Stage));
                CaptureSample();
            }

            if (_sampleStepCounter == 0 || previous.Stage != Sample.Stage || previous.BreakerTripped != Sample.BreakerTripped)
            {
                NotifyChanged();
            }
        }

        private void UpdateReplay(float elapsed)
        {
            if (_history.Count == 0)
            {
                return;
            }

            if (ReplayPlaying)
            {
                float speed = float.IsNaN(SimulationSpeed) ? 1f : Mathf.Clamp(SimulationSpeed, 0f, 3f);
                _replayCursor += elapsed * speed;
                if (_replayCursor >= _history[_history.Count - 1].Time)
                {
                    _replayCursor = _history[_history.Count - 1].Time;
                    ReplayPlaying = false;
                    SetMessage("复盘播放完成。");
                }

                ReplayIndex = FindReplayIndex(_replayCursor);
                DisplaySample = _history[ReplayIndex];
                NotifyChanged();
            }
        }

        private void EnsureModel()
        {
            if (_model == null)
            {
                _model = new JouleModel();
            }

            if (Parameters == null) Parameters = new JouleParameters();
        }

        private void ApplyParameterChange()
        {
            EnsureModel();
            _model.Reset(Parameters);
            PowerOn = false;
            Sample = _model.Sample;
            DisplaySample = Sample;
        }

        private void SetMessage(string message)
        {
            LastMessage = message;
            NotifyChanged();
        }

        private bool CanOperateLive()
        {
            if (!IsReviewing) return true;
            SetMessage("当前为复盘模式。请退出复盘或重置后再操作实验。");
            return false;
        }

        private int FindReplayIndex(float time)
        {
            int lower = 0;
            int upper = _history.Count - 1;
            while (lower < upper)
            {
                int middle = (lower + upper + 1) / 2;
                if (_history[middle].Time <= time) lower = middle;
                else upper = middle - 1;
            }

            return lower;
        }

        private void CaptureSample()
        {
            if (_history.Count > 0 && Math.Abs(_history[_history.Count - 1].Time - Sample.Time) < 0.00001f)
            {
                _history[_history.Count - 1] = Sample;
            }
            else
            {
                _history.Add(Sample);
                if (_history.Count > HistoryLimit) _history.RemoveAt(0);
            }
        }

        private static string StageName(MaterialStage stage)
        {
            switch (stage)
            {
                case MaterialStage.Warming: return "升温变色";
                case MaterialStage.Softened: return "绝缘软化";
                case MaterialStage.Pyrolysis: return "热解冒烟";
                case MaterialStage.Burning: return "明火燃烧";
                case MaterialStage.Charred: return "炭化";
                default: return "正常";
            }
        }

        private void RecordEvent(string message)
        {
            _events.Add(string.Format(CultureInfo.InvariantCulture, "{0:F2}s: {1}", Sample.Time, message));
            if (_events.Count > 256)
            {
                _events.RemoveAt(0);
            }
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
