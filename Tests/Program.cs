using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using SilverpineMods.CameraZoom;
using UnityEngine;
using UnityEngine.U2D;

internal static class Program
{
    private static int checks;
    private static void Main()
    {
        TestInput();
        TestProjectionAndLifecycle();
        TestFogResources();
        TestWeatherOverlays();
        TestNativePatch();
        Console.WriteLine($"PASS: {checks} assertions (managed camera model plus installed-game IL inspection). In-game rendering still requires a live check.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static void Near(float value, float expected, string message) => Check(Math.Abs(value - expected) < 0.0001f, message + $" ({value} != {expected})");
    private static object? Call(Type type, string name, object? target = null, params object[] args)
        => type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!.Invoke(target, args);
    private static void Tick(Camera camera) => Call(typeof(ZoomCamera), "LateUpdate", camera.GetComponent<ZoomCamera>());

    private static void TestInput()
    {
        foreach (string bad in new[] { "", "oops", "NaN", "Infinity", "0.19", "2.01", "1000", "1,000" })
            Check(!ZoomMath.TryParse(bad, out _), "Reject invalid direct input: " + bad);
        Check(ZoomMath.TryParse(" 0.8x ", out float entry) && entry == 0.8f, "Accept readable magnification input.");
        Check(ZoomMath.TryParse("1.24", out entry) && entry == 1.2f, "Direct entry snaps to a tenth.");
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Check(ZoomMath.TryParse("0,8", out entry) && entry == 0.8f, "Regional decimal comma.");
            Check(ZoomMath.TryParse("0.8", out entry) && entry == 0.8f, "Invariant decimal fallback.");
        }
        finally { CultureInfo.CurrentCulture = previous; }
        Near(ZoomMath.Normalize(float.NaN), 1, "Invalid configuration recovers.");
        Near(ZoomMath.Normalize(9), 2f, "Config clamps high.");
        Near(ZoomMath.Normalize(-9), 0.2f, "Config clamps low.");
        Near(ZoomMath.Normalize(0.35f), 0.4f, "Midpoints round consistently despite binary float precision.");
        float zoom = 0.2f;
        for (int i = 0; i < ZoomMath.SliderSteps; i++) zoom = ZoomMath.Normalize(zoom + 0.1f);
        Near(zoom, 2f, "Repeated increment reaches exact endpoint.");
        for (int i = 0; i < ZoomMath.SliderSteps; i++) zoom = ZoomMath.Normalize(zoom - 0.1f);
        Near(zoom, 0.2f, "Repeated decrement reaches exact endpoint.");
        Check(ZoomMath.TryParse("0.2", out entry) && entry == 0.2f, "Accept expanded lower endpoint.");
        Check(ZoomMath.TryParse("2.0", out entry) && entry == 2f, "Accept expanded upper endpoint.");
    }

    private static Camera Setup(bool pixelEnabled = true)
    {
        SaltExtraDebug.WorldTileOverview.IsActive = false;
        Player.Instance = new GameObject().AddComponent<Player>();
        Camera camera = new GameObject().AddComponent<Camera>();
        camera.transform.position = new Vector3(10, 20, -10);
        PixelPerfectCamera pixel = camera.gameObject.AddComponent<PixelPerfectCamera>();
        pixel.enabled = pixelEnabled;
        camera.orthographicSize = 5;
        camera.aspect = 16f / 9f;
        camera.rect = new Rect(0.1f, 0, 0.8f, 1);
        camera.forceIntoRenderTexture = true;
        Matrix4x4 projection = new();
        projection[0, 0] = 1 / (5 * camera.aspect);
        projection[1, 1] = 0.2f;
        projection[1, 2] = -0.1f;
        projection[2, 2] = -0.02f;
        projection[2, 3] = -1.002f;
        projection[3, 3] = 1;
        camera.projectionMatrix = projection;
        Camera.main = camera;
        Plugin.Zoom = 1;
        FOVManager.Instance = new();
        FOVSquare.all.Clear();
        FOVSquare.all.Add(new());
        ZoomCamera.ApplyMainCamera();
        return camera;
    }

    private static void TestProjectionAndLifecycle()
    {
        Camera camera = Setup();
        Matrix4x4 baseline = camera.projectionMatrix;
        Plugin.Zoom = 0.5f;
        Tick(camera);
        Near(camera.orthographicSize, 10, "Half magnification doubles visible height.");
        Near(camera.projectionMatrix[1, 1], 0.1f, "Custom matrix reflects zoom out.");
        Near(camera.projectionMatrix[1, 2], -0.05f, "Oblique shear scales with the view.");
        Near(camera.projectionMatrix[2, 2], baseline[2, 2], "Depth clipping is unchanged.");
        Check(!camera.GetComponent<PixelPerfectCamera>().enabled, "Suspend pixel snapping at fractional zoom.");
        Near(camera.rect.width, 0.8f, "Suspending pixel snapping preserves viewport.");
        Check(camera.forceIntoRenderTexture, "Preserve rendering flag changed by pixel snapping disable.");
        int writes = camera.ProjectionWrites;
        for (int frame = 0; frame < 300; frame++) Tick(camera);
        Near(camera.orthographicSize, 10, "Frames do not compound magnification.");
        Check(camera.ProjectionWrites == writes, "Idle frames do not rewrite camera projection.");
        Plugin.Zoom = 1.5f;
        Tick(camera);
        Near(camera.orthographicSize, 5 / 1.5f, "Zoom in uses the original baseline.");
        camera.aspect = 2;
        Tick(camera);
        Near(camera.projectionMatrix[0, 0], 0.15f, "Resize updates horizontal framing.");

        SaltExtraDebug.WorldTileOverview.IsActive = true;
        Matrix4x4 zoomed = camera.projectionMatrix;
        camera.orthographicSize = 52;
        camera.projectionMatrix = default;
        Tick(camera);
        Near(camera.orthographicSize, 52, "Whole-tile debug view has priority.");
        camera.orthographicSize = 5 / 1.5f;
        camera.projectionMatrix = zoomed;
        SaltExtraDebug.WorldTileOverview.IsActive = false;
        Plugin.Zoom = 1;
        Tick(camera);
        Near(camera.orthographicSize, 5, "Reset restores native magnification.");
        Near(camera.projectionMatrix[1, 2], -0.1f, "Reset restores native oblique shear.");
        Check(camera.GetComponent<PixelPerfectCamera>().enabled, "Reset restores enabled pixel snapping.");
        Plugin.Zoom = 0.8f;
        Tick(camera);
        camera.GetComponent<ZoomCamera>().enabled = false;
        Near(camera.orthographicSize, 5, "Camera controller teardown restores size.");
        Check(camera.GetComponent<PixelPerfectCamera>().enabled, "Controller teardown restores pixel snapping.");

        camera = Setup(false);
        Plugin.Zoom = 0.5f; Tick(camera);
        Plugin.Zoom = 1; Tick(camera);
        Check(!camera.GetComponent<PixelPerfectCamera>().enabled, "An already-disabled pixel component stays disabled.");
        Camera preview = new GameObject().AddComponent<Camera>();
        preview.orthographicSize = 0.6f;
        Plugin.Zoom = 0.5f; Tick(camera);
        Near(preview.orthographicSize, 0.6f, "Preview cameras are unaffected.");
        Player.Instance = null!; Tick(camera);
        Near(camera.orthographicSize, 5, "Leaving gameplay restores camera.");
        camera = Setup(); Plugin.Zoom = 0.8f; Tick(camera);
        Near(camera.orthographicSize, 6.25f, "A replacement scene camera gets a fresh baseline.");
        Check(camera.gameObject.GetComponents<ZoomCamera>().Length == 1, "Camera attachment is idempotent.");
        MainMenuUI.Instance.Open = true; Tick(camera);
        Near(camera.orthographicSize, 5, "Main-menu panorama restores its normal view.");
        Plugin.Zoom = 1.5f; Tick(camera);
        Near(camera.orthographicSize, 5, "Main-menu configuration does not zoom the background.");
        MainMenuUI.Instance.Open = false; Tick(camera);
        Near(camera.orthographicSize, 5 / 1.5f, "Entering gameplay applies saved settings without the plugin host.");
    }

    private static void TestFogResources()
    {
        Camera camera = Setup();
        Tick(camera);
        int normal = ZoomFog.Width() * ZoomFog.Height();
        Plugin.Zoom = 1.5f; Tick(camera);
        int close = ZoomFog.Width() * ZoomFog.Height();
        Check(close < normal, "Zooming in reduces the fog objects and per-turn raycast work.");
        Plugin.Zoom = 0.5f; Tick(camera);
        int far = ZoomFog.Width() * ZoomFog.Height();
        Check(far > normal && far < normal * 5, "Zoom out expands coverage within a reasonable footprint.");
        float depth = -3 - camera.transform.position.z;
        Vector3 lower = camera.ViewportToWorldPoint(new Vector3(0, 0, depth)) - camera.transform.position;
        Vector3 upper = camera.ViewportToWorldPoint(new Vector3(1, 1, depth)) - camera.transform.position;
        Check(-ZoomFog.OffsetX() <= lower.x && ZoomFog.Width() - ZoomFog.OffsetX() - 1 >= upper.x, "Fog covers full visible horizontal span.");
        Check(-ZoomFog.OffsetY() <= lower.y && ZoomFog.Height() - ZoomFog.OffsetY() - 1 >= upper.y, "Fog covers the oblique vertical span.");
        int initializations = FOVManager.Instance.Initializations;
        int conversions = camera.ViewConversions;
        for (int frame = 0; frame < 300; frame++)
        {
            camera.transform.position = new Vector3(frame, -frame, -10);
            Tick(camera);
        }
        Check(FOVManager.Instance.Initializations == initializations, "Following the player never rebuilds the fog grid.");
        Check(camera.ViewConversions == conversions, "Unchanged views skip footprint calculations.");
        camera.aspect = 2.13f; Tick(camera);
        Check(FOVManager.Instance.Initializations == initializations + 1, "A wider viewport rebuilds once.");
        Tick(camera);
        Check(FOVManager.Instance.Initializations == initializations + 1, "No repeated rebuilding after resize.");
        Console.WriteLine($"Fog model at 16:9: {far} cells at 0.5x, {normal} at 1.0x, {close} at 1.5x.");
        Plugin.Zoom = 0.2f; Tick(camera);
        int widest = ZoomFog.Width() * ZoomFog.Height();
        Check(widest > far && widest < 7000, "Expanded minimum stays within the visible footprint plus padding.");
        initializations = FOVManager.Instance.Initializations;
        for (int frame = 0; frame < 300; frame++) Tick(camera);
        Check(initializations == FOVManager.Instance.Initializations, "The expanded minimum does not rebuild per frame.");
        Plugin.Zoom = 2f; Tick(camera);
        Near(camera.orthographicSize, 2.5f, "Expanded maximum doubles magnification.");
    }

    private static void TestWeatherOverlays()
    {
        Camera camera = Setup();
        GameObject root = new() { name = "Weather" };
        root.transform.parent = camera.transform;
        root.transform.localPosition = new Vector3(0, 0, 10);
        var fogObject = new GameObject { name = "Weather_Fog" };
        fogObject.transform.parent = root.transform;
        fogObject.transform.localScale = new Vector3(27.3f, 8.05f, 1);
        var fog = fogObject.AddComponent<SpriteRenderer>();
        var snowObject = new GameObject { name = "SnowFog" };
        snowObject.transform.parent = root.transform;
        snowObject.transform.localPosition = new Vector3(-2.65f, 2.67f, 3.73f);
        snowObject.transform.localScale = new Vector3(27.3f, 8.05f, 1);
        var snow = snowObject.AddComponent<SpriteRenderer>();
        var rainObject = new GameObject();
        rainObject.transform.parent = root.transform;
        var rain = rainObject.AddComponent<ParticleSystem>();
        var worldEffect = new GameObject().AddComponent<ParticleSystem>();

        foreach (float zoom in new[] { 0.2f, 0.5f, 0.8f, 1.3f, 2f })
        {
            Plugin.Zoom = zoom; Tick(camera);
            foreach (SpriteRenderer overlay in new[] { fog, snow })
            {
                float depth = overlay.transform.position.z - camera.transform.position.z;
                Vector3 lower = camera.ViewportToWorldPoint(new Vector3(0, 0, depth));
                Vector3 upper = camera.ViewportToWorldPoint(new Vector3(1, 1, depth));
                Vector3 center = overlay.transform.position, size = overlay.bounds.size;
                Check(center.x - size.x / 2 <= lower.x && center.x + size.x / 2 >= upper.x,
                    "Fog covers both horizontal edges at " + zoom);
                Check(center.y - size.y / 2 <= lower.y && center.y + size.y / 2 >= upper.y,
                    "Fog covers both vertical edges at " + zoom);
            }
            Near(root.transform.localScale.y, 1 / zoom, "Weather coverage follows viewport magnification.");
            Check(rain.Mode == ParticleSystemScalingMode.Shape, "Weather expands spawn coverage only.");
            Check(rain.EmissionRate == 600 && rain.MaxParticles == 2000, "Particle work budget stays fixed.");
            Check(worldEffect.Mode == ParticleSystemScalingMode.Local, "World particles remain unchanged.");
        }
        int conversions = camera.ViewConversions;
        for (int frame = 0; frame < 300; frame++) Tick(camera);
        Check(camera.ViewConversions == conversions, "Idle weather does not refit every frame.");
        Plugin.Zoom = 1; Tick(camera);
        Near(root.transform.localScale.y, 1, "Reset restores weather root.");
        Near(fog.transform.localScale.y, 8.05f, "Reset restores fog size.");
        Near(snow.transform.localPosition.y, 2.67f, "Reset restores snow-fog offset.");
        Check(rain.Mode == ParticleSystemScalingMode.Local, "Reset restores particle scaling mode.");
        Plugin.Zoom = 0.2f; Tick(camera);
        camera.GetComponent<ZoomCamera>().enabled = false;
        Near(fog.transform.localScale.x, 27.3f, "Camera teardown restores overlay ownership.");
    }

    private static void TestNativePatch()
    {
        string gameRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        string gameAssembly = Path.Combine(gameRoot, "Silverpine_Data", "Managed", "Assembly-CSharp.dll");
        using AssemblyDefinition native = AssemblyDefinition.ReadAssembly(gameAssembly);
        var initialize = native.MainModule.Types.Single(t => t.Name == "FOVManager").Methods.Single(m => m.Name == "Initialize");
        int[] constants = initialize.Body.Instructions.Where(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_S)
            .Select(i => Convert.ToInt32(i.Operand)).Concat(initialize.Body.Instructions
                .Where(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_5).Select(_ => 5)).ToArray();
        Check(constants.OrderBy(x => x).SequenceEqual(new[] { 5, 10, 12, 20 }), "Installed game has exactly the expected fog grid constants.");
        var input = constants.Select(value => new CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_I4, value)).ToArray();
        var output = ((System.Collections.Generic.IEnumerable<CodeInstruction>)Call(typeof(ExpandFogPatch), "Transpiler", null, (object)input)!).ToArray();
        Check(output.All(i => i.opcode == System.Reflection.Emit.OpCodes.Call), "Transpiler substitutes all four dimensions.");
        int[] dimensions = output.Select(i => (int)((MethodInfo)i.operand).Invoke(null, null)!).ToArray();
        Check(dimensions.All(n => n > 0), "Transpiled getters provide valid current dimensions.");
        Check(native.MainModule.Types.Single(t => t.Name == "Player").Methods.Any(m => m.Name == "Start"), "Installed player attachment hook exists.");
    }
}
