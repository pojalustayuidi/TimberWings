// Assets/_Project/Editor/IslandV2/IslandLookCheck.cs
// Остров v2, этап 3: проверка света, неба и дымки против профилей бюджета. Меню: Tools/BuilderSim/Локация/Остров v2/7. Проверить свет и дымку (приоритет 126).
// Части этапа 3: IslandLookSetup.cs (подготовка, пункты 124 и 125), IslandLookCheck.cs (этот файл).
//
// Вне Play Mode: статическая проверка (ассет Cloud Candy и его числа, шейдеры, согласованность профилей).
// В Play Mode (окно Game должно быть видно): то же плюс замер. 45 кадров с дымкой, потом 20 кадров без неё (IslandLook.hazeDebugEnabled = false),
// разница Batches и Tris сравнивается с лимитом профиля (maxExtraBatches, maxExtraTris). Профиль берётся по GameSettings.LightQuality:
// Низкое = Mobile Low, Среднее = Mobile High, Высокое = ПК. Кроме этого сверяются дальность теней и Bloom с профилем.
// Результат пишется в Console и копируется в буфер обмена.
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IslandV2
{
    public static class IslandLookCheck
    {
        static int ok, warn, fail;
        static StringBuilder sb;

        static void Ok(string s) { ok++; sb.AppendLine("  [ок] " + s); }
        static void Warn(string s) { warn++; sb.AppendLine("  [!] " + s); }
        static void Fail(string s) { fail++; sb.AppendLine("  [НЕТ] " + s); }

        // ---------------------------------------------------------------- состояние замера
        static int frames, step;
        static bool probing;
        static int aBatches, aTris, aSetPass, aShadowCasters;
        static int bBatches, bTris, bSetPass;

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/7. Проверить свет и дымку (замер в Play Mode)", false, 126)]
        static void Run()
        {
            ok = warn = fail = 0;
            sb = new StringBuilder("IslandV2: проверка света и дымки (этап 3)\n");

            var style = CloudCandyStyle.Current;
            StaticChecks(style);

            if (!Application.isPlaying)
            {
                sb.AppendLine("  Замер Batches и Tris пропущен: запусти Play Mode (окно Game видно) и вызови пункт ещё раз.");
                Finish();
                return;
            }
            if (style == null) { Finish(); return; }
            if (probing) return;

            if (IslandLook.Instance == null)
            {
                Fail("в игре нет IslandLook (нужны FloatingIsland и игрок в сцене)");
                Finish();
                return;
            }
            probing = true; frames = 0; step = 0;
            IslandLook.Instance.hazeDebugEnabled = true;
            EditorApplication.update += Tick;
            Debug.Log("IslandV2: замер света и дымки пошёл (~70 кадров), подожди.");
        }

        static void Finish()
        {
            sb.AppendLine("Итого: ок " + ok + ", предупреждений " + warn + ", провалов " + fail + ".");
            if (fail > 0) Debug.LogError(sb.ToString()); else if (warn > 0) Debug.LogWarning(sb.ToString()); else Debug.Log(sb.ToString());
            GUIUtility.systemCopyBuffer = sb.ToString();
        }

        // ---------------------------------------------------------------- статика (без игры)
        static void StaticChecks(CloudCandyStyle st)
        {
            if (st == null)
            {
                Fail("нет ассета Cloud Candy или выключен «Style Enabled»: этап 3 не работает (пункт 5 создаст ассет)");
                return;
            }
            Ok("ассет Cloud Candy найден и включён");

            if (Shader.Find("Project/IslandV2/Haze") != null) Ok("шейдер дымки Project/IslandV2/Haze найден");
            else Fail("шейдер Project/IslandV2/Haze не найден (файл Shaders/IslandV2/IslandHaze.shader)");

            if (Shader.Find("Project/IslandV2/VertexColor") != null) Ok("шейдер земли Project/IslandV2/VertexColor найден");
            else Fail("шейдер земли не найден");

            CheckProfile("Mobile Low", st.budgetMobileLow, st.low);
            CheckProfile("Mobile High", st.budgetMobileHigh, st.medium);
            CheckProfile("ПК", st.budgetPc, st.high);

            // монотонность: слабее профиль не должен быть тяжелее
            if (st.budgetMobileLow.hazeCards <= st.budgetMobileHigh.hazeCards && st.budgetMobileHigh.hazeCards <= st.budgetPc.hazeCards) Ok("число карточек дымки растёт от Mobile Low к ПК");
            else Warn("число карточек дымки не растёт от Mobile Low к ПК: " + st.budgetMobileLow.hazeCards + " / " + st.budgetMobileHigh.hazeCards + " / " + st.budgetPc.hazeCards);

            float eff = st.fogDensityDay * st.fogMulIsland;
            float fog100 = 1f - Mathf.Exp(-eff * 100f);
            sb.AppendLine("  туман острова днём: плотность " + eff.ToString("0.0000") + " (Exponential), на 100 м закрывает около " + (fog100 * 100f).ToString("0") + "%");
            if (st.fogMode != FogMode.Exponential) Warn("режим тумана " + st.fogMode + ": числа плотности подбирались под Exponential");

            if (st.haze.nearFadeEnd <= st.haze.nearFadeStart) Warn("haze: nearFadeEnd должен быть больше nearFadeStart");
            if (st.haze.farFadeEnd <= st.haze.farFadeStart) Warn("haze: farFadeEnd должен быть больше farFadeStart");
            if (st.haze.alpha > 0.5f) Warn("haze.alpha " + st.haze.alpha.ToString("0.00") + ": это уже туман, а не лёгкая дымка");

            var urp = new StringBuilder();
            IslandLookSetup.AppendUrp(urp);
            sb.Append(urp);
        }

        static void CheckProfile(string name, CloudCandyStyle.IslandBudget b, CloudCandyStyle.Quality q)
        {
            if (b.hazeCards < 0 || b.hazeCards > 64) Fail(name + ": hazeCards " + b.hazeCards + " вне 0..64");
            if (b.shadowDistance > b.shadowDistanceMax) Fail(name + ": дальность теней " + b.shadowDistance.ToString("0") + " м больше лимита " + b.shadowDistanceMax.ToString("0") + " м");
            else Ok(name + ": дальность теней " + b.shadowDistance.ToString("0") + " м (лимит " + b.shadowDistanceMax.ToString("0") + ")");
            if (q.bloomOn && !b.bloomAllowed) Warn(name + ": в блоке «Три качества» Bloom включён, а профиль его не разрешает");
            else Ok(name + ": Bloom " + (q.bloomOn ? "включён" : "выключен") + ", профиль " + (b.bloomAllowed ? "разрешает" : "не разрешает"));
        }

        // ---------------------------------------------------------------- замер (Play Mode)
        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; probing = false; return; }
            var look = IslandLook.Instance;
            if (look == null) { EditorApplication.update -= Tick; probing = false; return; }
            frames++;

            if (step == 0 && frames >= 45)
            {
                aBatches = UnityStats.batches; aTris = UnityStats.triangles; aSetPass = UnityStats.setPassCalls; aShadowCasters = UnityStats.shadowCasters;
                look.hazeDebugEnabled = false;
                step = 1; frames = 0;
            }
            else if (step == 1 && frames >= 20)
            {
                bBatches = UnityStats.batches; bTris = UnityStats.triangles; bSetPass = UnityStats.setPassCalls;
                look.hazeDebugEnabled = true;
                EditorApplication.update -= Tick; probing = false;
                Evaluate(look);
            }
        }

        static void Evaluate(IslandLook look)
        {
            var st = CloudCandyStyle.Current;
            if (st == null) { Fail("стиль выключился во время замера"); Finish(); return; }

            LightQualityLevel q = GameSettings.LightQuality;
            var b = st.GetBudget(q);
            string pname = CloudCandyStyle.BudgetName(q);
            sb.AppendLine("  профиль: " + pname + " (качество света " + q + "), уровень качества Unity «" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "»");
            sb.AppendLine("  с дымкой: Batches " + aBatches + ", SetPass " + aSetPass + ", Tris " + aTris + ", Shadow casters " + aShadowCasters);
            sb.AppendLine("  без дымки: Batches " + bBatches + ", SetPass " + bSetPass + ", Tris " + bTris);

            int dB = aBatches - bBatches, dT = aTris - bTris;
            if (dB <= b.maxExtraBatches) Ok("дымка добавила " + dB + " Batches (лимит " + b.maxExtraBatches + ")");
            else Fail("дымка добавила " + dB + " Batches, лимит " + b.maxExtraBatches + ". Нет инстансинга? Проверь, что в Graphics стоит GPU instancing и шейдер собран");
            if (dT <= b.maxExtraTris) Ok("дымка добавила " + dT + " Tris (лимит " + b.maxExtraTris + ")");
            else Fail("дымка добавила " + dT + " Tris, лимит " + b.maxExtraTris);

            if (look.HazeCardCount <= b.hazeCards) Ok("карточек дымки " + look.HazeCardCount + " (профиль до " + b.hazeCards + ")");
            else Fail("карточек " + look.HazeCardCount + " больше профиля " + b.hazeCards);

            // в URP реальную дальность теней задаёт URP Asset (LightingRig меняет и его, и QualitySettings)
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float sd = urpAsset != null ? urpAsset.shadowDistance : QualitySettings.shadowDistance;
            if (sd <= b.shadowDistanceMax + 0.5f) Ok("дальность теней сейчас " + sd.ToString("0") + " м (лимит " + b.shadowDistanceMax.ToString("0") + ")");
            else Fail("дальность теней сейчас " + sd.ToString("0") + " м, лимит профиля " + b.shadowDistanceMax.ToString("0") + ". Включён ли useBudgetShadowDistance? Есть ли в сцене LightingRig?");

            var stack = VolumeManager.instance.stack;
            var bloom = stack != null ? stack.GetComponent<Bloom>() : null;
            bool bloomOn = bloom != null && bloom.IsActive() && bloom.intensity.value > 0.001f;
            if (bloomOn && !b.bloomAllowed) Fail("Bloom сейчас включён (интенсивность " + bloom.intensity.value.ToString("0.00") + "), а профиль " + pname + " его не разрешает");
            else Ok("Bloom сейчас " + (bloomOn ? "включён" : "выключен") + ", профиль " + (b.bloomAllowed ? "разрешает" : "не разрешает"));

            Light sun = RenderSettings.sun;
            if (sun != null) sb.AppendLine("  солнце: тени " + sun.shadows + ", сила " + sun.intensity.ToString("0.00") + ", цвет " + ColorUtility.ToHtmlStringRGB(sun.color));
            sb.AppendLine("  туман: плотность " + RenderSettings.fogDensity.ToString("0.0000") + ", цвет " + ColorUtility.ToHtmlStringRGB(RenderSettings.fogColor));
            sb.AppendLine("  Замер зависит от того, что видит камера: сделай его с игровой камеры и один раз с OrbitCamera сверху.");
            Finish();
        }
    }
}
