using UnityEngine;

namespace MRLab.Joule
{
    /// <summary>
    /// Circuit geometry in ExperimentBench local metres. A location is a wire id
    /// and normalized arc length, shared by rendering and any PC or XR adapter.
    /// </summary>
    public static class WirePathGeometry
    {
        public const int WireCount = 4;
        private static readonly Vector3[][] Paths =
        {
            new[] { new Vector3(-.94f, .943f, -.093f), new Vector3(-.68f, .91f, -.19f), new Vector3(-.56f, .92f, -.22f), new Vector3(-.38f, .93f, -.16f), new Vector3(-.22f, .934f, -.13f) },
            new[] { new Vector3(-.22f, .94f, -.13f), new Vector3(-.12f, .957f, -.10f), new Vector3(.03f, .96f, -.10f), new Vector3(.15f, .951f, -.12f), new Vector3(.24f, .94f, -.13f) },
            new[] { new Vector3(.24f, .934f, -.13f), new Vector3(.4f, .934f, -.16f), new Vector3(.51f, .93f, -.08f), new Vector3(.67f, .94f, -.063f) },
            new[] { new Vector3(1.03f, .94f, -.063f), new Vector3(.87f, .916f, -.19f), new Vector3(.76f, .916f, -.28f), new Vector3(.35f, .916f, -.30f), new Vector3(-.08f, .916f, -.29f), new Vector3(-.55f, .91f, -.33f), new Vector3(-1.01f, .943f, -.093f) }
        };
        private static readonly float[] Lengths = CalculateLengths();

        public static Vector3[] Points(int wire)
        {
            return (Vector3[])Paths[Mathf.Clamp(wire, 0, WireCount - 1)].Clone();
        }

        public static Vector3 Point(int wire, float normalized)
        {
            wire = Mathf.Clamp(wire, 0, WireCount - 1);
            if (float.IsNaN(normalized) || float.IsInfinity(normalized)) normalized = .5f;
            Vector3[] points = Paths[wire];
            float remaining = Mathf.Clamp01(normalized) * Lengths[wire];
            for (int i = 0; i < points.Length - 1; i++)
            {
                float length = Vector3.Distance(points[i], points[i + 1]);
                if (remaining <= length) return Vector3.Lerp(points[i], points[i + 1], remaining / length);
                remaining -= length;
            }
            return points[points.Length - 1];
        }

        public static bool ClosestPoint(Vector3 point, out int wire, out float normalized,
            out Vector3 snapped, float maxDistance = .12f)
        {
            wire = 1;
            normalized = .5f;
            snapped = Point(wire, normalized);
            if (!IsFinite(point) || float.IsNaN(maxDistance) || maxDistance < 0f) return false;
            float bestDistance = float.PositiveInfinity;
            for (int w = 0; w < WireCount; w++)
            {
                Vector3[] points = Paths[w];
                float traversed = 0f;
                for (int i = 0; i < points.Length - 1; i++)
                {
                    Vector3 segment = points[i + 1] - points[i];
                    float length = segment.magnitude;
                    float t = Mathf.Clamp01(Vector3.Dot(point - points[i], segment) / segment.sqrMagnitude);
                    Vector3 candidate = points[i] + segment * t;
                    float distance = (candidate - point).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        wire = w;
                        normalized = (traversed + length * t) / Lengths[w];
                        snapped = candidate;
                    }
                    traversed += length;
                }
            }
            return bestDistance <= maxDistance * maxDistance;
        }

        public static string Name(int wire)
        {
            switch (wire)
            {
                case 0: return "电源正极导线";
                case 2: return "负载连接导线";
                case 3: return "回路导线";
                default: return "实验加热导线";
            }
        }

        private static float[] CalculateLengths()
        {
            var lengths = new float[WireCount];
            for (int w = 0; w < WireCount; w++)
                for (int i = 0; i < Paths[w].Length - 1; i++)
                    lengths[w] += Vector3.Distance(Paths[w][i], Paths[w][i + 1]);
            return lengths;
        }

        private static bool IsFinite(Vector3 point)
        {
            return !float.IsNaN(point.x) && !float.IsInfinity(point.x)
                && !float.IsNaN(point.y) && !float.IsInfinity(point.y)
                && !float.IsNaN(point.z) && !float.IsInfinity(point.z);
        }
    }
}
