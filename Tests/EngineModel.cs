// A managed model for lifecycle and projection regression checks; not a Unity renderer.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
    public struct Matrix4x4 : IEquatable<Matrix4x4>
    {
        private float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33;
        public float this[int row, int col]
        {
            get => (row * 4 + col) switch
            {
                0 => m00, 1 => m01, 2 => m02, 3 => m03, 4 => m10, 5 => m11, 6 => m12, 7 => m13,
                8 => m20, 9 => m21, 10 => m22, 11 => m23, 12 => m30, 13 => m31, 14 => m32, 15 => m33,
                _ => throw new IndexOutOfRangeException()
            };
            set
            {
                switch (row * 4 + col)
                {
                    case 0: m00 = value; break; case 1: m01 = value; break; case 2: m02 = value; break; case 3: m03 = value; break;
                    case 4: m10 = value; break; case 5: m11 = value; break; case 6: m12 = value; break; case 7: m13 = value; break;
                    case 8: m20 = value; break; case 9: m21 = value; break; case 10: m22 = value; break; case 11: m23 = value; break;
                    case 12: m30 = value; break; case 13: m31 = value; break; case 14: m32 = value; break; case 15: m33 = value; break;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }
        public bool Equals(Matrix4x4 other)
        {
            for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++)
                if (this[row, col] != other[row, col]) return false;
            return true;
        }
        public static bool operator ==(Matrix4x4 a, Matrix4x4 b) => a.Equals(b);
        public static bool operator !=(Matrix4x4 a, Matrix4x4 b) => !a.Equals(b);
        public override bool Equals(object? other) => other is Matrix4x4 matrix && Equals(matrix);
        public override int GetHashCode() => m00.GetHashCode();
    }
    public sealed class Transform
    {
        public GameObject Owner = null!;
        private Transform? parentValue;
        public readonly List<Transform> Children = new();
        public Vector3 localPosition, localScale = new(1, 1, 1);
        public Transform? parent
        {
            get => parentValue;
            set { parentValue?.Children.Remove(this); parentValue = value; value?.Children.Add(this); }
        }
        public Vector3 lossyScale => parent == null ? localScale : Multiply(parent.lossyScale, localScale);
        public Vector3 position
        {
            get => parent == null ? localPosition : parent.position + Multiply(parent.lossyScale, localPosition);
            set
            {
                if (parent == null) localPosition = value;
                else { Vector3 p = value - parent.position, s = parent.lossyScale; localPosition = new(p.x / s.x, p.y / s.y, p.z / s.z); }
            }
        }
        private static Vector3 Multiply(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);
        public Transform Find(string name) => Children.FirstOrDefault(c => c.Owner.name == name)!;
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class =>
            Owner.GetComponents<T>().Concat(Children.SelectMany(c => c.GetComponentsInChildren<T>(includeInactive))).ToArray();
    }
    public sealed class GameObject
    {
        private readonly List<Component> components = new();
        public readonly Transform transform = new();
        public string name = "";
        public GameObject() { transform.Owner = this; }
        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            components.Add(component);
            return component;
        }
        public T GetComponent<T>() where T : class => components.OfType<T>().FirstOrDefault()!;
        public T[] GetComponents<T>() => components.OfType<T>().ToArray();
    }
    public class Component
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
        public string name => gameObject.name;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
        public T[] GetComponents<T>() => gameObject.GetComponents<T>();
    }
    public class Behaviour : Component
    {
        private bool active = true;
        public bool enabled
        {
            get => active;
            set
            {
                if (active == value) return;
                active = value;
                GetType().GetMethod(value ? "OnEnable" : "OnDisable", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(this, null);
            }
        }
    }
    public class MonoBehaviour : Behaviour { }
    public struct Bounds { public Vector3 size; }
    public sealed class SpriteRenderer : Behaviour
    {
        public Bounds bounds => new() { size = transform.lossyScale };
    }
    public enum ParticleSystemScalingMode { Hierarchy, Local, Shape }
    public sealed class ParticleSystem : Component
    {
        public ParticleSystemScalingMode Mode = ParticleSystemScalingMode.Local;
        public float EmissionRate = 600;
        public int MaxParticles = 2000;
        public MainModule main => new(this);
        public readonly struct MainModule
        {
            private readonly ParticleSystem system;
            public MainModule(ParticleSystem system) { this.system = system; }
            public ParticleSystemScalingMode scalingMode { get => system.Mode; set => system.Mode = value; }
        }
    }
    public sealed class Camera : Behaviour
    {
        public static Camera main = null!;
        public bool orthographic = true, forceIntoRenderTexture;
        public float orthographicSize, aspect;
        public Rect rect = new(0, 0, 1, 1);
        private Matrix4x4 projection;
        public int ProjectionWrites, ViewConversions;
        public Matrix4x4 projectionMatrix { get => projection; set { projection = value; ProjectionWrites++; } }
        public Vector3 ViewportToWorldPoint(Vector3 point)
        {
            ViewConversions++;
            // Inverse of the orthographic/sheared projection used by Silverpine.
            return new Vector3(transform.position.x + (point.x * 2 - 1 + projection[0, 2] * point.z - projection[0, 3]) / projection[0, 0],
                transform.position.y + (point.y * 2 - 1 + projection[1, 2] * point.z - projection[1, 3]) / projection[1, 1],
                transform.position.z + point.z);
        }
    }
}
namespace UnityEngine.U2D
{
    public sealed class PixelPerfectCamera : UnityEngine.Behaviour
    {
        private void OnDisable()
        {
            var camera = GetComponent<UnityEngine.Camera>();
            camera.orthographicSize = 1;
            camera.rect = new UnityEngine.Rect(0, 0, 1, 1);
            camera.forceIntoRenderTexture = false;
        }
    }
}
public sealed class Player : UnityEngine.MonoBehaviour { public static Player Instance = null!; private void Start() { } }
public sealed class MainMenuUI
{
    public static MainMenuUI Instance = new();
    public bool Open;
    public bool IsOpen() => Open;
}
public sealed class FOVManager
{
    public static FOVManager Instance = new();
    public int Initializations;
    public void Initialize() => Initializations++;
}
public sealed class FOVSquare { public static HashSet<FOVSquare> all = new(); }
namespace SaltExtraDebug
{
    internal static class WorldTileOverview { internal static bool IsActive { get; set; } }
}
namespace SilverpineMods.CameraZoom
{
    internal static class Plugin { internal static float Zoom = 1f; }
}
