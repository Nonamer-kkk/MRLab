using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MRLab.Joule
{
    /// <summary>
    /// Opt-in runtime benchmark. It creates itself only when the launched player
    /// contains --joule-benchmark, so normal editor and PC runs are unchanged.
    /// </summary>
    public sealed class JoulePerformanceProbe : MonoBehaviour
    {
        private const float WarmupSeconds = 5f;
        private const float MeasurementSeconds = 30f;
        private const float CaptureAtSeconds = 8f;

        private readonly List<float> frameTimes = new List<float>(2400);
        private float startedAt;
        private bool captureRequested;
        private bool captureEnabled;
        private bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfRequested()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            bool benchmark = HasArgument(arguments, "--joule-benchmark");
            if (!benchmark)
                return;

            var existing = FindObjectOfType<JoulePerformanceProbe>();
            if (existing != null)
                return;

            var go = new GameObject("JoulePerformanceProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<JoulePerformanceProbe>().captureEnabled = HasArgument(arguments, "--joule-capture");
        }

        private static bool HasArgument(string[] arguments, string expected)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void Start()
        {
            startedAt = Time.unscaledTime;
            Debug.Log("JOULE_BENCHMARK: warm-up started; measurement begins after 5 seconds.");
        }

        private void Update()
        {
            if (finished)
                return;

            float elapsed = Time.unscaledTime - startedAt;
            if (captureEnabled && !captureRequested && elapsed >= CaptureAtSeconds)
            {
                CaptureScreenshot();
                captureRequested = true;
            }

            if (elapsed < WarmupSeconds)
                return;

            if (elapsed < WarmupSeconds + MeasurementSeconds)
            {
                float frameTime = Time.unscaledDeltaTime;
                if (frameTime > 0f && !float.IsNaN(frameTime) && !float.IsInfinity(frameTime))
                    frameTimes.Add(frameTime);
                return;
            }

            WriteReport();
            finished = true;
        }

        private void CaptureScreenshot()
        {
            string directory = ResultsDirectory();
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Laboratory.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("JOULE_CAPTURE: screenshot requested at " + path);
        }

        private void WriteReport()
        {
            string directory = ResultsDirectory();
            Directory.CreateDirectory(directory);

            frameTimes.Sort();
            float totalSeconds = 0f;
            for (int i = 0; i < frameTimes.Count; i++)
                totalSeconds += frameTimes[i];

            double meanFps = totalSeconds > 0f ? frameTimes.Count / (double)totalSeconds : 0d;
            float p95Milliseconds = 0f;
            if (frameTimes.Count > 0)
            {
                int p95Index = Mathf.Clamp(Mathf.CeilToInt(frameTimes.Count * 0.95f) - 1, 0, frameTimes.Count - 1);
                p95Milliseconds = frameTimes[p95Index] * 1000f;
            }

            var report = new PerformanceReport
            {
                timestamp_utc = DateTime.UtcNow.ToString("O"),
                fps_mean = (float)meanFps,
                frame_time_p95_ms = p95Milliseconds,
                sample_count = frameTimes.Count,
                measurement_seconds = MeasurementSeconds,
                resolution = Screen.width + "x" + Screen.height,
                screen_width = Screen.width,
                screen_height = Screen.height,
                cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName,
                gpu_memory_mb = SystemInfo.graphicsMemorySize,
                system_memory_mb = SystemInfo.systemMemorySize,
                unity_version = Application.unityVersion,
                platform = Application.platform.ToString()
            };

            string path = Path.Combine(directory, "Performance.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("JOULE_BENCHMARK: report written to " + path +
                      " (mean FPS " + report.fps_mean.ToString("0.00") +
                      ", p95 frame " + report.frame_time_p95_ms.ToString("0.00") +
                      " ms, samples " + report.sample_count + ")");
        }

        private static string ResultsDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "Results");
        }

        [Serializable]
        private sealed class PerformanceReport
        {
            public string timestamp_utc;
            public float fps_mean;
            public float frame_time_p95_ms;
            public int sample_count;
            public float measurement_seconds;
            public string resolution;
            public int screen_width;
            public int screen_height;
            public string cpu;
            public string gpu;
            public int gpu_memory_mb;
            public int system_memory_mb;
            public string unity_version;
            public string platform;
        }
    }
}
