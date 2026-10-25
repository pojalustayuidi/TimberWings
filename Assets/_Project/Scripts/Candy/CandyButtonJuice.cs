// Assets/_Project/Scripts/UI/Candy/CandyButtonJuice.cs (НЕ в Editor)
// «Желейная» кнопка: пружинит при наведении, приплющивается при нажатии и подпрыгивает при отпускании,
// на клик сыплет искры, на блике мигает искорка. Недоступная кнопка на клик «мотает головой».
// Можно включить «привлекание внимания»: раз в несколько секунд кнопка качнётся и подпрыгнет (например «Забрать награду»).
// Вешается на объект с Button/Toggle (или любой Image). Меню: Tools > BuilderSim > UI > Candy Kit > Fun > 2. Оживить все кнопки.
// Звуки: события onHover / onClick (подключите свои клипы).
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CandyButtonJuice : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Ссылки")]
    public CandyFxKit kit;
    [Tooltip("Что пружинит. Пусто = этот объект")]
    public RectTransform target;

    [Header("Пружина")]
    public float hoverScale = 1.06f;
    public float pressScaleX = 1.05f;
    public float pressScaleY = 0.9f;
    public float stiffness = 320f;
    public float damping = 11f;
    [Tooltip("Подпрыгивание при клике (0 = выключить)")]
    public float clickKick = 1.6f;

    [Header("Эффекты")]
    public bool twinkleOnHover = true;
    public bool burstOnClick = true;
    public int burstCount = 7;
    public bool ringOnClick = false;
    public Color fxColor = new Color(1f, 0.92f, 0.55f, 1f);

    [Header("Привлечь внимание")]
    public bool attention = false;
    public float attentionEvery = 5f;

    [Header("Звук")]
    public UnityEvent onHover;
    public UnityEvent onClick;

    Selectable sel;
    CandySpring sx, sy, rot;
    Vector3 baseScale = Vector3.one;
    Quaternion baseRot = Quaternion.identity;
    bool hover, down, captured;
    float attTimer;

    bool Interactable { get { return sel == null || sel.IsInteractable(); } }
    RectTransform Rt { get { return target != null ? target : (RectTransform)transform; } }

    void Awake()
    {
        sel = GetComponent<Selectable>();
        EnsureEvents();
    }

    // Кнопки создаются из кода через AddComponent: UnityEvent там может остаться null, а EventSystem дёргает
    // OnPointerEnter/OnPointerClick сразу. Создаём события сами и ниже вызываем их через ?. (NullReferenceException на onHover)
    void EnsureEvents()
    {
        if (onHover == null) onHover = new UnityEvent();
        if (onClick == null) onClick = new UnityEvent();
    }

    void OnEnable()
    {
        EnsureEvents();
        if (sel == null) sel = GetComponent<Selectable>();
        baseScale = Rt.localScale;
        baseRot = Rt.localRotation;
        captured = true;
        sx = new CandySpring(1f);
        sy = new CandySpring(1f);
        rot = new CandySpring(0f);
        hover = down = false;
        attTimer = attentionEvery * Random.Range(0.5f, 1f);
    }

    void OnDisable()
    {
        if (!captured) return;
        Rt.localScale = baseScale;
        Rt.localRotation = baseRot;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        bool ok = Interactable;

        if (attention && ok && !hover && !down)
        {
            attTimer -= dt;
            if (attTimer <= 0f)
            {
                attTimer = attentionEvery * Random.Range(0.8f, 1.2f);
                rot.v = 240f * (Random.value < 0.5f ? -1f : 1f);
                sx.v += 1.1f;
                sy.v += 1.1f;
            }
        }

        float tx = 1f, ty = 1f;
        if (ok && down) { tx = pressScaleX; ty = pressScaleY; }
        else if (ok && hover) { tx = ty = hoverScale; }

        // всё улеглось: не трогаем Transform, чтобы не мешать другим скриптам
        if (sx.Settled(tx) && sy.Settled(ty) && rot.Settled(0f)) return;

        sx.Step(tx, stiffness, damping, dt);
        sy.Step(ty, stiffness, damping, dt);
        rot.Step(0f, 260f, 9f, dt);

        var rt = Rt;
        rt.localScale = new Vector3(baseScale.x * sx.x, baseScale.y * sy.x, baseScale.z);
        rt.localRotation = baseRot * Quaternion.Euler(0f, 0f, rot.x);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        hover = true;
        if (!Interactable) return;
        if (onHover != null) onHover.Invoke();
        if (twinkleOnHover && kit != null) CandyFx.Twinkle(kit, Rt, fxColor);
    }

    public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
    public void OnPointerDown(PointerEventData e) { down = true; }
    public void OnPointerUp(PointerEventData e)
    {
        down = false;
        if (!captured) return;   // пружины создаются в OnEnable
        if (Interactable && clickKick > 0f) { sy.v += clickKick; sx.v -= clickKick * 0.5f; }
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!Interactable)
        {
            if (captured) rot.v = 320f * (Random.value < 0.5f ? -1f : 1f);   // «нельзя»: кнопка мотает головой
            return;
        }
        if (onClick != null) onClick.Invoke();
        if (kit == null) return;
        if (burstOnClick) CandyFx.Burst(kit, Rt, fxColor, burstCount);
        if (ringOnClick) CandyFx.Ring(kit, Rt, fxColor);
    }
}
