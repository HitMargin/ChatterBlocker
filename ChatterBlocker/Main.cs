using HarmonyLib;
using September;
using UnityModManagerNet;
using UnityEngine;

namespace ChatterBlocker;

public static class Main
{
    public static UnityModManager.ModEntry Mod { get; private set; }

    internal static int ChatterBlockInterval
    {
        get => PlayerPrefs.GetInt("chatterBlockInterval", 0);
        set
        {
            PlayerPrefs.SetInt("chatterBlockInterval", value);
            PlayerPrefs.Save();
        }
    }

    static bool Load(UnityModManager.ModEntry modEntry)
    {
        Mod = modEntry;
        modEntry.OnToggle = OnToggle;
        modEntry.OnHideGUI = _ => PlayerPrefs.Save();
        modEntry.OnSaveGUI = _ => PlayerPrefs.Save();
        return true;
    }

    private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
    {
        if (value)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            PatchManager.Initialize(harmony);

            PatchManager.RegisterPatches(() => true,
                typeof(Patches.Patch_GenerateSettings),
                typeof(Patches.Patch_RDString));

            PatchManager.RegisterLazyPatches(
                () => scrController.instance != null,
                typeof(Patches.Patch_Start),
                typeof(Patches.Patch_Restart),
                typeof(Patches.Patch_HookCallback),
                typeof(Patches.Patch_UpdateInput),
                typeof(Patches.Patch_CountValidKeys),
                typeof(Patches.Patch_UpdateSetting));

            PatchManager.ApplyAllAsync();
        }
        else
        {
            PatchManager.UnpatchAll();
        }
        return true;
    }
}