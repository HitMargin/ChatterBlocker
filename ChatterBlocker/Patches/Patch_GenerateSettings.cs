using System;
using System.Collections.Generic;
using HarmonyLib;
using September;
using UnityEngine;

namespace ChatterBlocker.Patches;

// ── 3. SettingsMenu.GenerateSettings → 追加去抖设置按钮 ──
[HarmonyPatch(typeof(SettingsMenu), "GenerateSettings")]
internal static class Patch_GenerateSettings
{
    [HarmonyPostfix]
    internal static void Postfix(SettingsMenu __instance)
    {
        // Use PatchManager field refs to access private fields
        var getTabs = PatchManager.CreateFieldRef<SettingsMenu, List<List<PauseSettingButton>>>("settingsTabs");
        var getTabButtons = PatchManager.CreateFieldRef<SettingsMenu, List<SettingsTabButton>>("tabButtons");
        var getContent = PatchManager.CreateFieldRef<SettingsMenu, RectTransform>("settingsScrollRectContent");
        var getPrefab = PatchManager.CreateFieldRef<SettingsMenu, GameObject>("buttonPrefab");

        var settingsTabs = getTabs(__instance);
        var tabButtons = getTabButtons(__instance);
        var content = getContent(__instance);
        var buttonPrefab = getPrefab(__instance);

        if (settingsTabs == null || settingsTabs.Count == 0 || buttonPrefab == null)
            return;

        // Check if already added (GenerateSettings may be called multiple times)
        foreach (var tab in settingsTabs)
            foreach (var btn in tab)
                if (btn.name == "chatterBlockInterval")
                    return;

        // Instantiate button
        var go = UnityEngine.Object.Instantiate(buttonPrefab, content);
        var comp = go.GetComponent<PauseSettingButton>();
        comp.name = "chatterBlockInterval";
        comp.type = "Int";
        comp.minInt = 0;
        comp.maxInt = 2000;
        comp.changeBy = 5;
        comp.changeBySmall = 1;
        comp.unit = "ms";
        comp.hasRange = true;
        comp.descriptionKey = "pauseMenu.settings.info.chatterBlockInterval";
        comp.hasDescription = true;

        // Find the "advanced" tab by name, fallback to first tab
        int targetTab = 0;
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i].name == "advanced")
            {
                targetTab = i;
                break;
            }
        }
        var targetList = settingsTabs[targetTab];

        // Insert right after "keyLimiter" button
        var keyLimiter = targetList.Find(b => b.name == "keyLimiter");
        if (keyLimiter != null)
        {
            int idx = targetList.IndexOf(keyLimiter);
            targetList.Insert(idx + 1, comp);
            comp.transform.SetSiblingIndex(keyLimiter.transform.GetSiblingIndex() + 1);
        }
        else
        {
            targetList.Add(comp);
        }

        __instance.UpdateSetting(comp, SettingsMenu.Interaction.Refresh);
        comp.SetFocus(false);
        comp.label.text = RDString.Get("pauseMenu.settings.chatterBlockInterval");
        comp.label.SetLocalizedFont();
        comp.valueLabel.SetLocalizedFont();
    }
}
