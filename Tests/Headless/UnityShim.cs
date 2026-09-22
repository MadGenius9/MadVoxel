// Executable stand-ins for the sliver of UnityEngine that MadVoxel's engine-agnostic
// code touches. This exists so the voxel, content, inventory, crafting and save layers
// can be RUN outside the Unity editor, not merely compiled. It is a test-only file and
// lives outside Assets/, so Unity never sees it.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name = "";
        public override string ToString() { return name; }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() { return new T(); }
    }

    public class Component : Object { }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }

    public static class Debug
    {
        public static readonly List<string> Errors = new List<string>();
        public static readonly List<string> Warnings = new List<string>();

        public static void Log(object message) { Console.WriteLine("       [log] " + message); }
        public static void LogFormat(string format, params object[] args) { Log(string.Format(format, args)); }
        public static void LogWarning(object message) { Warnings.Add(message.ToString()); }
        public static void LogWarningFormat(string format, params object[] args) { LogWarning(string.Format(format, args)); }
        public static void LogError(object message) { Errors.Add(message.ToString()); }
        public static void LogErrorFormat(string format, params object[] args) { LogError(string.Format(format, args)); }
    }

    public static class Application
    {
        public static string persistentDataPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MadVoxelHeadless");
    }

    public static class Resources
    {
        // No asset database outside Unity, so the content always comes from code.
        public static T Load<T>(string path) where T : Object { return null; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color grey { get { return new Color(0.5f, 0.5f, 0.5f); } }
        public static Color white { get { return new Color(1f, 1f, 1f); } }
        public static Color clear { get { return new Color(0f, 0f, 0f, 0f); } }
        public static Color operator *(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, c.a); }
        public static Color Lerp(Color x, Color y, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
        }
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static bool operator ==(Vector2Int a, Vector2Int b) { return a.x == b.x && a.y == b.y; }
        public static bool operator !=(Vector2Int a, Vector2Int b) { return !(a == b); }
        public bool Equals(Vector2Int o) { return this == o; }
        public override bool Equals(object o) { return o is Vector2Int && Equals((Vector2Int)o); }
        public override int GetHashCode() { return x * 73856093 ^ y * 19349663; }
        public override string ToString() { return string.Format("({0},{1})", x, y); }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public float this[int i]
        {
            get { return i == 0 ? x : (i == 1 ? y : z); }
            set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; }
        }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public float magnitude { get { return Mathf.Sqrt(sqrMagnitude); } }
        public void Normalize() { float m = magnitude; if (m > 1e-6f) { x /= m; y /= m; z /= m; } }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator *(Vector3 a, float f) { return new Vector3(a.x * f, a.y * f, a.z * f); }
        public static Vector3 operator /(Vector3 a, float f) { return new Vector3(a.x / f, a.y / f, a.z / f); }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        }
        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public override string ToString() { return string.Format("({0},{1},{2})", x, y, z); }
    }

    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3Int one { get { return new Vector3Int(1, 1, 1); } }
        public static Vector3Int up { get { return new Vector3Int(0, 1, 0); } }
        public static Vector3Int FloorToInt(Vector3 v)
        {
            return new Vector3Int(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));
        }
        public static Vector3Int operator +(Vector3Int a, Vector3Int b) { return new Vector3Int(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static bool operator ==(Vector3Int a, Vector3Int b) { return a.x == b.x && a.y == b.y && a.z == b.z; }
        public static bool operator !=(Vector3Int a, Vector3Int b) { return !(a == b); }
        public bool Equals(Vector3Int o) { return this == o; }
        public override bool Equals(object o) { return o is Vector3Int && Equals((Vector3Int)o); }
        public override int GetHashCode() { return x * 73856093 ^ y * 19349663 ^ z * 83492791; }
        public override string ToString() { return string.Format("({0},{1},{2})", x, y, z); }
    }

    public struct RectInt
    {
        public int xMin, yMin, width, height;
        public RectInt(int x, int y, int width, int height)
        {
            xMin = x; yMin = y; this.width = width; this.height = height;
        }
        public int xMax { get { return xMin + width; } }
        public int yMax { get { return yMin + height; } }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int CeilToInt(float f) { return (int)Math.Ceiling(f); }
        public static int RoundToInt(float f) { return (int)Math.Round(f, MidpointRounding.AwayFromZero); }
        public static float Abs(float f) { return Math.Abs(f); }
        public static int Abs(int f) { return Math.Abs(f); }
        public static float Sin(float f) { return (float)Math.Sin(f); }
        public static float Cos(float f) { return (float)Math.Cos(f); }
        public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public static float Min(float a, float b) { return Math.Min(a, b); }
        public static int Min(int a, int b) { return Math.Min(a, b); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
        public static int Max(int a, int b) { return Math.Max(a, b); }
        public static float Clamp01(float f) { return f < 0f ? 0f : (f > 1f ? 1f : f); }
        public static float Clamp(float f, float lo, float hi) { return f < lo ? lo : (f > hi ? hi : f); }
        public static int Clamp(int f, int lo, int hi) { return f < lo ? lo : (f > hi ? hi : f); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float InverseLerp(float a, float b, float v) { return a == b ? 0f : Clamp01((v - a) / (b - a)); }
        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t); t = t * t * (3f - 2f * t); return from + (to - from) * t;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class CreateAssetMenuAttribute : Attribute
    {
        public string menuName { get; set; }
        public string fileName { get; set; }
        public int order { get; set; }
    }

    public class PropertyAttribute : Attribute { }
    public class HeaderAttribute : PropertyAttribute { public HeaderAttribute(string header) { } }
    public class TooltipAttribute : PropertyAttribute { public TooltipAttribute(string tooltip) { } }
    public class RangeAttribute : PropertyAttribute { public RangeAttribute(float min, float max) { } }
    public class TextAreaAttribute : PropertyAttribute { public TextAreaAttribute() { } public TextAreaAttribute(int min, int max) { } }
    public class SerializeFieldAttribute : Attribute { }
    public class HideInInspectorAttribute : Attribute { }
}
