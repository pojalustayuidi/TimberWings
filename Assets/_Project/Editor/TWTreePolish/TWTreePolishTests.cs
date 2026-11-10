using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TWTreePolishTests
{
    [MenuItem("Tools/BuilderSim/UI/Полировка рубки/4. Тесты подсказки и наград", false, 243)]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("TREE POLISH TESTS: останови Play."); return; }
        var errors = new List<string>();
        Action<bool, string> check = (ok, title) => { if (!ok) errors.Add(title); };
        for (int mask = 0; mask < 16; mask++)
        {
            bool chop = (mask & 8) != 0, walk = (mask & 4) != 0, hover = (mask & 2) != 0, near = (mask & 1) != 0;
            var expected = chop ? TreeFocusRules.Kind.Chopping : walk ? TreeFocusRules.Kind.Walking : hover ? TreeFocusRules.Kind.Hover : near ? TreeFocusRules.Kind.Nearby : TreeFocusRules.Kind.None;
            check(TreeFocusRules.Priority(chop, walk, hover, near) == expected, "Приоритет цели " + mask);
        }
        check(TreeFocusRules.InRange(Vector3.zero,new Vector3(12f,50f,0f),12f),"Дерево на границе 12 м доступно для подсветки, высота не учитывается");
        check(!TreeFocusRules.InRange(Vector3.zero,new Vector3(12.1f,0f,0f),12f),"Дерево за 12 м не подсвечивается при наведении");
        check(!TreeFocusRules.InRange(Vector3.zero,Vector3.zero,0f),"Нулевая дальность безопасно выключает подсветку");
        check(TreeFocusRules.InRange(Vector3.zero,new Vector3(28f,0f,0f),32f),"Дерево на автоподходе сохраняет выделение до 32 м");
        check(TreeFocusRules.Fits(4, 1) == 1, "Один свободный слот: база 4, влезет 1");
        check(TreeFocusRules.Fits(2, 50) == 2, "Свободное место не повышает базовую награду");
        check(TreeFocusRules.Fits(4, 0) == 0 && TreeFocusRules.Fits(4, -2) == 0, "Полный/переполненный рюкзак");
        Vector3 a = Vector3.zero, b = new Vector3(4, 8, 0), c = new Vector3(10, 1, 0);
        check(TreeFocusRules.Flight(a, b, c, 0) == a && TreeFocusRules.Flight(a, b, c, 1) == c, "Точные концы полёта");
        check(Vector3.Distance(TreeFocusRules.Flight(a,b,c,.0001f), a) < .0001f, "Мягкий старт полёта");
        check(Vector3.Distance(TreeFocusRules.Flight(a,b,c,.9999f), c) < .0001f, "Мягкое прибытие");
        var go = new GameObject("TW_TreePolish_TestOnly") { hideFlags = HideFlags.HideAndDontSave };
        go.SetActive(false); // Do not touch the live ChopRewards singleton, HUD, inventory or save state.
        try
        {
            // Reproduce the builder's Edit Mode add path without touching the live controller.
            var focus = Undo.AddComponent<TreePolishController>(go);
            check(focus != null, "Компонент добавляется через Undo вне Play без создания native-объектов в конструкторе");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var ensureBlocks = typeof(TreePolishController).GetMethod("EnsureRenderBlocks", flags);
            ensureBlocks.Invoke(focus, null);
            foreach (var fieldName in new[] { "groundBlock", "drawBlock", "sourceBlock", "rendererBlock" })
            {
                var field = typeof(TreePolishController).GetField(fieldName, flags);
                var first = field.GetValue(focus);
                check(first is MaterialPropertyBlock, "Инициализация " + fieldName);
                ensureBlocks.Invoke(focus, null);
                check(ReferenceEquals(first, field.GetValue(focus)), "Повторная инициализация сохраняет " + fieldName);
            }
            var service = go.AddComponent<ChopRewards>(); service.xpDelay = 10;
            int logs = 0, xp = 0;
            var logMethod = typeof(ChopRewards).GetMethod("FlyLog", flags);
            var logRoutine = (IEnumerator)logMethod.Invoke(service, new object[] { null, null, Vector3.zero, null, 10f, (Action)(() => logs++) });
            check(logRoutine.MoveNext(), "Полёт дошёл до своей исходной задержки");
            (logRoutine as IDisposable).Dispose(); (logRoutine as IDisposable).Dispose();
            check(logs == 1, "Отмена ожидающего полёта: бревно начисляется ровно один раз");
            var fallback = (IEnumerator)logMethod.Invoke(service, new object[] { null, null, Vector3.zero, null, 0f, (Action)(() => logs++) });
            check(!fallback.MoveNext(), "Нет Canvas/цели: безопасный запасной путь");
            (fallback as IDisposable).Dispose(); check(logs == 2, "Без HUD второе бревно начисляется ровно один раз");
            var xpMethod = typeof(ChopRewards).GetMethod("XpRoutine", flags);
            var xpRoutine = (IEnumerator)xpMethod.Invoke(service, new object[] { 2, (Action)(() => xp++) });
            check(xpRoutine.MoveNext(), "XP сохраняет исходную задержку");
            (xpRoutine as IDisposable).Dispose(); (xpRoutine as IDisposable).Dispose();
            check(xp == 1, "Отмена XP: колбэк ровно один раз");
        }
        catch (Exception ex) { errors.Add(ex.ToString()); }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        if (errors.Count > 0) Debug.LogError("TREE POLISH TESTS: " + errors.Count + " ошибок\n" + string.Join("\n",errors));
        else Debug.Log("TREE POLISH TESTS OK: добавление компонента в редакторе, повторная инициализация render-блоков, 16 комбинаций цели, вместимость, плавность полёта, отмена и отсутствие HUD. Инвентарь и сейв не затрагивались.");
    }
}
