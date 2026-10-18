using System.Collections.Generic;
using UnityEngine;

namespace SilverpineMods.CameraZoom;

// Fit native camera-attached weather without touching world effects or lighting.
internal sealed class ZoomWeather
{
    private sealed class Overlay
    {
        internal readonly SpriteRenderer Renderer;
        internal Transform Transform => Renderer.transform;
        internal readonly Vector3 Position, Scale;
        internal readonly float Depth;
        internal Overlay(SpriteRenderer renderer, Camera camera)
        {
            Renderer = renderer;
            Position = Transform.localPosition;
            Scale = Transform.localScale;
            Depth = Transform.position.z - camera.transform.position.z;
        }
    }

    private readonly List<Overlay> overlays = new();
    private readonly List<(ParticleSystem System, ParticleSystemScalingMode Mode)> particles = new();
    private readonly Transform? weatherRoot;
    private readonly Vector3 rootScale;
    private readonly Matrix4x4 originalProjection;
    private Matrix4x4 lastProjection;
    private bool fitted;

    internal ZoomWeather(Camera camera)
    {
        Transform weather = camera.transform.Find("Weather");
        if (weather == null) return;
        weatherRoot = weather;
        rootScale = weather.localScale;
        originalProjection = camera.projectionMatrix;
        foreach (SpriteRenderer renderer in weather.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if ((renderer.name == "Weather_Fog" || renderer.name == "SnowFog") &&
                renderer.bounds.size.x > 0f && renderer.bounds.size.y > 0f)
                overlays.Add(new Overlay(renderer, camera));
        }
        foreach (ParticleSystem system in weather.GetComponentsInChildren<ParticleSystem>(true))
            particles.Add((system, system.main.scalingMode));
    }

    internal void Fit(Camera camera)
    {
        Matrix4x4 projection = camera.projectionMatrix;
        if (fitted && projection == lastProjection) return;
        lastProjection = projection;
        fitted = true;
        if (weatherRoot != null)
        {
            weatherRoot.localScale = new Vector3(rootScale.x * originalProjection[0, 0] / projection[0, 0],
                rootScale.y * originalProjection[1, 1] / projection[1, 1], rootScale.z);
            foreach (var particle in particles)
            {
                if (particle.System == null) continue;
                var main = particle.System.main;
                // Resize spawn coverage, retaining droplet/flake size, speed,
                // emission rate and maximum particle count. No resimulation loop.
                main.scalingMode = ParticleSystemScalingMode.Shape;
            }
        }
        foreach (Overlay overlay in overlays)
        {
            if (overlay.Renderer == null) continue;
            Vector3 lower = camera.ViewportToWorldPoint(new Vector3(0f, 0f, overlay.Depth));
            Vector3 upper = camera.ViewportToWorldPoint(new Vector3(1f, 1f, overlay.Depth));
            // Small overscan keeps texture/filter edges outside the visible image.
            float width = (upper.x - lower.x) * 1.02f;
            float height = (upper.y - lower.y) * 1.02f;
            overlay.Transform.localScale = overlay.Scale;
            Vector3 size = overlay.Renderer.bounds.size;
            if (size.x <= 0f || size.y <= 0f) continue;
            overlay.Transform.localScale = new Vector3(overlay.Scale.x * width / size.x,
                overlay.Scale.y * height / size.y, overlay.Scale.z);
            overlay.Transform.position = new Vector3((upper.x + lower.x) * 0.5f,
                (upper.y + lower.y) * 0.5f, overlay.Transform.position.z);
        }
    }

    internal void Restore()
    {
        if (!fitted) return;
        if (weatherRoot != null) weatherRoot.localScale = rootScale;
        foreach (var particle in particles)
        {
            if (particle.System == null) continue;
            var main = particle.System.main;
            main.scalingMode = particle.Mode;
        }
        foreach (Overlay overlay in overlays)
        {
            if (overlay.Renderer == null) continue;
            overlay.Transform.localPosition = overlay.Position;
            overlay.Transform.localScale = overlay.Scale;
        }
        fitted = false;
    }
}
