using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ChatterBlocker.Patches;

// ── RDString.GetWithCheck → Provides the translated text──
[HarmonyPatch(typeof(RDString), "GetWithCheck")]
internal static class Patch_RDString
{
    [HarmonyPrefix]
    internal static bool Prefix(string key, out bool exists, Dictionary<string, object> parameters, ref string __result)
    {
        exists = false;
        if (_translations.TryGetValue(key, out var langs)
            && langs.TryGetValue(RDString.language, out var text))
        {
            exists = true;
            __result = text;
            return false;
        }
        // Fallback to English
        if (_translations.TryGetValue(key, out langs)
            && langs.TryGetValue(SystemLanguage.English, out text))
        {
            exists = true;
            __result = text;
            return false;
        }
        return true;
    }

    private static readonly Dictionary<string, Dictionary<SystemLanguage, string>> _translations = new()
    {
        ["pauseMenu.settings.chatterBlockInterval"] = new()
        {
            { SystemLanguage.English, "Chatter Blocker" },
            { SystemLanguage.Korean, "채터링 방지" },
            { SystemLanguage.ChineseSimplified, "键盘防抖" },
            { SystemLanguage.ChineseTraditional, "鍵盤防抖" },
            { SystemLanguage.Japanese, "チャタリング防止" },
            { SystemLanguage.Spanish, "Antirrebote de teclado" },
            { SystemLanguage.Portuguese, "Antirrebote do teclado" },
            { SystemLanguage.French, "Antirebond du clavier" },
            { SystemLanguage.German, "Tastatur-Entprellung" },
            { SystemLanguage.Russian, "Подавление дребезга клавиатуры" },
            { SystemLanguage.Polish, "Filtr antychatteringowy" },
            { SystemLanguage.Romanian, "Filtru anti-chattering" },
            { SystemLanguage.Vietnamese, "Chống rung phím" },
            { SystemLanguage.Czech, "Potlačení zákmitů klávesnice" },
        },
        ["pauseMenu.settings.info.chatterBlockInterval"] = new()
        {
            { SystemLanguage.English, "Minimum interval between key-down events. Set to 0 to disable." },
            { SystemLanguage.Korean, "키 입력 이벤트의 최소 간격입니다. 0으로 설정하면 비활성화됩니다." },
            { SystemLanguage.ChineseSimplified, "设定键盘按键的最小触发间隔（毫秒），设为0关闭此功能。" },
            { SystemLanguage.ChineseTraditional, "設定鍵盤按鍵的最小觸發間隔（毫秒），設為0關閉此功能。" },
            { SystemLanguage.Japanese, "キー入力の最小間隔（ミリ秒）を設定します。0で無効になります。" },
            { SystemLanguage.Spanish, "Intervalo mínimo entre pulsaciones. Establézcalo en 0 para desactivar." },
            { SystemLanguage.Portuguese, "Intervalo mínimo entre pressionamentos de tecla. Defina como 0 para desativar." },
            { SystemLanguage.French, "Intervalle minimum entre les pressions de touches. Mettez à 0 pour désactiver." },
            { SystemLanguage.German, "Mindestintervall zwischen Tastenanschlägen. Auf 0 setzen, um zu deaktivieren." },
            { SystemLanguage.Russian, "Минимальный интервал между нажатиями клавиш. Установите 0 для отключения." },
            { SystemLanguage.Polish, "Minimalny odstęp między naciśnięciami klawiszy. Ustaw 0, aby wyłączyć." },
            { SystemLanguage.Romanian, "Interval minim între apăsări de taste. Setați la 0 pentru a dezactiva." },
            { SystemLanguage.Vietnamese, "Khoảng thời gian tối thiểu giữa các lần nhấn phím. Đặt 0 để tắt." },
            { SystemLanguage.Czech, "Minimální interval mezi stisky kláves. Nastavte 0 pro vypnutí." },
        },
    };
}
