// Assets/_Project/Scripts/UI/Core/UiClickFallback.cs (НЕ в Editor)
// Страховка для кнопок HUD: если EventSystem не донёс клик (сломан модуль ввода, кнопку перекрыл невидимый элемент),
// клик обрабатывается по координатам курсора или пальца, как в SettingsMenu.
// Как не нажать дважды: кнопка при регистрации получает слушатель, который запоминает кадр клика. Страховка срабатывает
// только если в кадр отпускания EventSystem клика не сделал. Если сверху под курсором другая настоящая кнопка или окно
// (любой Selectable), страховка молчит. В Console одна строка [UiClick] объясняет, кто перекрыл кнопку.
// Использование: UiClickFallback.Register(кнопка, () => условие_активности, modalOk);
//   modalOk = true для кнопок самого модального окна (большая карта, продажа): им можно работать, когда MobileInput.ModalOpen.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UiClickFallback : MonoBehaviour
{
    class Entry
    {
        public Button b;
        public Func<bool> gate;
        public bool modalOk;
        public int clickFrame = -1;
    }

    static UiClickFallback inst;
    static readonly List<Entry> entries = new List<Entry>();
    static readonly List<RaycastResult> hits = new List<RaycastResult>();
    static int logged;
    Entry pressed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { inst = null; entries.Clear(); logged = 0; }

    public static void Register(Button b, Func<bool> gate = null, bool modalOk = false)
    {
        if (b == null) return;
        if (inst == null)
        {
            var go = new GameObject("UiClickFallback");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<UiClickFallback>();
        }
        var e = new Entry { b = b, gate = gate, modalOk = modalOk };
        b.onClick.AddListener(() => e.clickFrame = Time.frameCount);
        entries.Add(e);
    }

    // курсор или палец: нажатие, отпускание, позиция
    static bool ReadPointer(out Vector2 pos, out bool down, out bool up)
    {
        down = up = false;
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            pos = t.position;
            down = t.phase == TouchPhase.Began;
            up = t.phase == TouchPhase.Ended;
            return down || up;
        }
        pos = Input.mousePosition;
        down = Input.GetMouseButtonDown(0);
        up = Input.GetMouseButtonUp(0);
        return down || up;
    }

    static Camera CamOf(Canvas cv)
    {
        return cv != null && cv.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cv.rootCanvas.worldCamera : null;
    }

    static Entry Pick(Vector2 pos)
    {
        Entry best = null;
        int bestOrder = int.MinValue;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e.b == null) { entries.RemoveAt(i); continue; }
            if (!e.b.isActiveAndEnabled || !e.b.IsInteractable()) continue;
            if (e.gate != null && !e.gate()) continue;
            if (MobileInput.ModalOpen && !e.modalOk) continue;
            var rt = (RectTransform)e.b.transform;
            Canvas cv = e.b.targetGraphic != null ? e.b.targetGraphic.canvas : e.b.GetComponentInParent<Canvas>();
            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, pos, CamOf(cv))) continue;
            int order = cv == null ? 0 : (cv.overrideSorting ? cv.sortingOrder : cv.rootCanvas.sortingOrder);
            if (best == null || order > bestOrder) { best = e; bestOrder = order; }
        }
        return best;
    }

    void LateUpdate()
    {
        if (entries.Count == 0) return;
        Vector2 pos;
        bool down, up;
        if (!ReadPointer(out pos, out down, out up)) return;

        if (down) pressed = Pick(pos);
        if (!up) return;

        var p = pressed;
        pressed = null;
        if (p == null || p.b == null) return;
        if (p.clickFrame == Time.frameCount) return;          // EventSystem справился сам
        if (Pick(pos) != p) return;                           // палец ушёл с кнопки

        // что EventSystem видит под курсором. Другой настоящий Selectable сверху (окно, кнопка): уступаем ему
        string top = "ничего";
        var es = EventSystem.current;
        if (es != null)
        {
            hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = pos }, hits);
            if (hits.Count > 0 && hits[0].gameObject != null)
            {
                var go = hits[0].gameObject;
                top = go.name;
                if (!go.transform.IsChildOf(p.b.transform) && go.GetComponentInParent<Selectable>() != null) return;
            }
        }
        if (logged < 8)
        {
            logged++;
            Debug.Log("[UiClick] EventSystem не нажал «" + p.b.name + "» (" + (es == null ? "EventSystem нет" : "сверху: " + top) + "), клик выполнен по координатам");
        }
        p.b.onClick.Invoke();
    }
}
