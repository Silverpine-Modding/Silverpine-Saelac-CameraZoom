using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Silverpine.ModdingTools;

namespace SilverpineMods.CameraZoom;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(Silverpine.ModdingTools.Plugin.PluginGuid, "1.10.0")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "Saelac.Silverpine.CameraZoom";
    public const string PluginName = "Camera Zoom";
    public const string PluginVersion = "1.0.1";
    internal static ConfigEntry<float> ZoomSetting = null!;
    internal static float Zoom => ZoomMath.Normalize(ZoomSetting.Value);

    private void Awake()
    {
        ZoomSetting = Config.Bind("Camera", "Zoom", 1f,
            new ConfigDescription("World magnification: 0.2 = farthest out, 1.0 = normal, 2.0 = closest in. Steps of 0.1.",
                new AcceptableValueRange<float>(ZoomMath.Minimum, ZoomMath.Maximum)));
        ZoomSetting.Value = Zoom;
        ZoomSetting.SettingChanged += (_, _) =>
        {
            if (ZoomSetting.Value != Zoom) ZoomSetting.Value = Zoom;
            ZoomCamera.ApplyMainCamera();
        };
        ModdingToolsMenu.RegisterSession(PluginGuid + ".settings", PluginName,
            (_, session) => ZoomWindow.Open(session), order: 320);
        InventoryModTools.RegisterSession(PluginGuid + ".controls", PluginName,
            (_, session) => ZoomWindow.Open(session), order: 320);
        new Harmony(PluginGuid).PatchAll(typeof(Plugin).Assembly);
        SerializationManager.On2FramesAfterFinishedLoading += ZoomCamera.ApplyMainCamera;
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Zoom {Zoom:0.0}x; controls in Modding Tools and inventory Mods.");
    }

    // Static registrations and configuration survive Silverpine's bootstrap-host destruction.
    internal static void SetZoom(float value) => ZoomSetting.Value = ZoomMath.Normalize(value);
}
