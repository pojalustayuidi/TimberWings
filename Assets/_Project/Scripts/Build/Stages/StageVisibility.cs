// Assets/_Project/Scripts/Build/Stages/StageVisibility.cs   (НЕ в папке Editor!)
// «Эта группа видна только на стадиях от minStage до maxStage». Стадии HutStages накапливаются (SetStage(3) показывает Stage1+2+3),
// поэтому то, что должно исчезнуть при стройке (разобранные брёвна и доски нулевой стадии, колышки разметки, пила на земле),
// вешается на отдельную группу с этим компонентом: maxStage = 1 значит «видно только пока здание в самом начале».
//
// Как исчезает: если стадия сменилась в игре, группа не пропадает щелчком, а «уходит в стройку»: детали по очереди
// взлетают дугой к точке flyTarget (центр хижины), уменьшаются и гаснут с облачком пыли. При загрузке сохранения, в редакторе
// и при «Уменьшить эффекты» всё скрывается сразу. Вернуть стадию назад (тест в редакторе) возвращает детали на места.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Stage Visibility")]
public class StageVisibility : MonoBehaviour
{
    [Tooltip("С какой стадии группа видна (1..3)")]
    [Range(1, 3)] public int minStage = 1;
    [Tooltip("До какой стадии включительно видна. 0 = до конца игры")]
    [Range(0, 3)] public int maxStage = 0;
    [Tooltip("Куда летят детали, когда группа уходит. Пусто = просто уменьшаются на месте")]
    public Transform flyTarget;
    [Tooltip("Сколько секунд занимает уход всей группы")]
    public float exitTime = 1.4f;
    public enum ExitStyle { Fly, Sink, Pop }
    [Tooltip("Fly = детали летят к flyTarget; Sink = уходят в землю (забор); Pop = подпрыгивают и схлопываются на месте (строители)")]
    public ExitStyle exitStyle = ExitStyle.Fly;

    class Item { public Transform t; public Vector3 pos, scale; public Quaternion rot; public float delay; }
    readonly List<Item> items = new List<Item>();
    bool captured;
    Coroutine routine;

    public bool Visible(int stage) { return stage >= minStage && (maxStage <= 0 || stage <= maxStage); }

    /// <summary>Вызывает HutStages при смене стадии. animate: плавный уход (только в игре, не при загрузке сохранения).</summary>
    public void Apply(int stage, bool animate)
    {
        bool show = Visible(stage);
        if (routine != null) { StopCoroutine(routine); routine = null; }
        Capture();
        RestoreAll();

        if (show) { gameObject.SetActive(true); return; }

        bool wasOn = gameObject.activeSelf;
        bool canAnim = animate && wasOn && Application.isPlaying && isActiveAndEnabled && gameObject.activeInHierarchy
                       && !VibeSettings.ReduceEffects && items.Count > 0;
        if (!canAnim) { gameObject.SetActive(false); return; }
        routine = StartCoroutine(ExitRoutine());
    }

    // запоминаем «домашнюю» позу каждой видимой детали (верхний уровень: те, у кого есть Renderer и нет такого же родителя)
    void Capture()
    {
        if (captured) return;
        captured = true;
        items.Clear();
        var rs = GetComponentsInChildren<Renderer>(true);
        var set = new HashSet<Transform>();
        for (int i = 0; i < rs.Length; i++)
            if (rs[i] is MeshRenderer) set.Add(rs[i].transform);

        var list = new List<Transform>();
        foreach (var t in set)
        {
            bool nested = false;
            for (var p = t.parent; p != null && p != transform; p = p.parent)
                if (set.Contains(p)) { nested = true; break; }
            if (!nested) list.Add(t);
        }
        // сверху вниз: сначала уходит то, что лежит выше (куча «разбирается» сверху)
        list.Sort((a, b) => b.position.y.CompareTo(a.position.y));
        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i];
            items.Add(new Item { t = t, pos = t.localPosition, rot = t.localRotation, scale = t.localScale, delay = list.Count > 1 ? (float)i / (list.Count - 1) : 0f });
        }
    }

    void RestoreAll()
    {
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it.t == null) continue;
            it.t.localPosition = it.pos;
            it.t.localRotation = it.rot;
            it.t.localScale = it.scale;
        }
    }

    IEnumerator ExitRoutine()
    {
        float dur = Mathf.Max(0.2f, exitTime);
        float piece = Mathf.Min(0.6f, dur * 0.5f);       // сколько живёт одна деталь
        float room = Mathf.Max(0.01f, dur - piece);
        float t0 = Time.time;
        Vector3 target = flyTarget != null ? flyTarget.position : Vector3.zero;
        bool hasTarget = flyTarget != null;
        float lastFx = 0f;

        while (true)
        {
            float el = Time.time - t0;
            bool all = true;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.t == null) continue;
                float k = (el - it.delay * room) / piece;
                if (k <= 0f) { all = false; continue; }
                if (k >= 1f) { it.t.localScale = Vector3.zero; continue; }
                all = false;
                float e = k * k * (3f - 2f * k);
                // дуга: летит к цели и немного вверх, уменьшаясь
                Vector3 wp = it.t.parent != null ? it.t.parent.TransformPoint(it.pos) : it.pos;
                if (exitStyle == ExitStyle.Sink)
                {
                    it.t.position = wp + Vector3.down * (0.9f * e);
                    it.t.localScale = it.scale * (1f - Mathf.SmoothStep(0.55f, 1f, e));
                    it.t.localRotation = it.rot * Quaternion.Euler(0f, 0f, Mathf.Sin(e * 10f) * 7f * (1f - e));
                }
                else if (exitStyle == ExitStyle.Pop)
                {
                    it.t.position = wp + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.35f);
                    it.t.localScale = it.scale * (1f - e * e);
                    it.t.localRotation = it.rot;
                }
                else
                {
                    Vector3 to = hasTarget ? target + Vector3.up * 1.4f : wp + Vector3.up * 1.2f;
                    Vector3 mid = Vector3.Lerp(wp, to, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 1.1f);
                    it.t.position = mid;
                    it.t.localScale = it.scale * (1f - e);
                    it.t.localRotation = it.rot * Quaternion.Euler(0f, e * 160f, e * 40f);
                }

                if (exitStyle != ExitStyle.Sink && ImpactFx.Instance != null && Time.time - lastFx > 0.12f && k < 0.2f)
                {
                    lastFx = Time.time;
                    ImpactFx.Instance.HammerTick(wp);
                }
            }
            if (all) break;
            yield return null;
        }

        RestoreAll();
        gameObject.SetActive(false);
        routine = null;
    }

    void OnDisable()
    {
        // группу выключили извне посреди ухода: оставляем детали на своих местах, чтобы потом не показать «сломанную» кучу
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
            RestoreAll();
        }
    }

    /// <summary>Для HutStages: применить ко всем группам внутри здания.</summary>
    public static void ApplyAll(Transform root, int stage, bool animate)
    {
        if (root == null) return;
        var all = root.GetComponentsInChildren<StageVisibility>(true);
        for (int i = 0; i < all.Length; i++) all[i].Apply(stage, animate);
    }
}
