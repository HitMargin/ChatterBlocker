using HarmonyLib;
using UnityEngine;

namespace ChatterBlocker.Patches;

// ── SettingsMenu.UpdateSetting → Handles reading and writing of chatterBlockInterval ──
[HarmonyPatch(typeof(SettingsMenu), "UpdateSetting")]
internal static class Patch_UpdateSetting
{
    [HarmonyPrefix]
    internal static bool Prefix(PauseSettingButton setting, SettingsMenu.Interaction action)
    {
        if (setting.name != "chatterBlockInterval")
            return true; // let original handle other settings

        // Activate / ActivateInfo do nothing for Int type
        if (action == SettingsMenu.Interaction.Activate || action == SettingsMenu.Interaction.ActivateInfo)
            return false;

        int val = Main.ChatterBlockInterval;

        if (action == SettingsMenu.Interaction.Increment)
        {
            int change = RDInput.holdingShift && setting.changeBySmall > 0
                ? setting.changeBySmall : (setting.changeBy > 0 ? setting.changeBy : 1);
            val = Mathf.Min(val + change, setting.maxInt);
        }
        else if (action == SettingsMenu.Interaction.Decrement)
        {
            int oldVal = val;
            int change = RDInput.holdingShift && setting.changeBySmall > 0
                ? setting.changeBySmall : (setting.changeBy > 0 ? setting.changeBy : 1);
            val = Mathf.Max(val - change, setting.minInt);
            if (val <= 0 && oldVal > 0)
                ChatterBlocker.Reset();
        }

        if (action == SettingsMenu.Interaction.Increment || action == SettingsMenu.Interaction.Decrement)
        {
            if (val != Main.ChatterBlockInterval)
                Main.ChatterBlockInterval = val;

            // Arrow animation + sound effect
            bool isInc = action == SettingsMenu.Interaction.Increment;
            setting.PlayArrowAnimation(isInc);
            var pauseMenu = scrController.instance?.pauseMenu;
            if (pauseMenu != null)
                pauseMenu.PlayMenuSfx(isInc ? SfxSound.MenuIncrement : SfxSound.MenuDecrement);
        }

        // Refresh display
        setting.valueLabel.text = val.ToString() + "ms";
        setting.leftArrow.transform.localScale = (val <= setting.minInt) ? Vector3.zero : Vector3.one;
        setting.rightArrow.transform.localScale = (val >= setting.maxInt) ? Vector3.zero : Vector3.one;

        if (action == SettingsMenu.Interaction.Refresh)
            setting.initialValue = val;

        return false;
    }
}
