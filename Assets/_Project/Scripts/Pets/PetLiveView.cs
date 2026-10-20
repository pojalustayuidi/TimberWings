// Assets/_Project/Scripts/Pets/PetLiveView.cs (НЕ в Editor)
// Живой питомец в интерфейсе: RawImage, в который камера мини-студии (PetStage) рисует вращающуюся модель с анимацией Idle.
// Создаётся через PetLiveView.Create (его вызывает PetCardView.EnableLive). Пока объект выключен (окно закрыто),
// студия выключена и ничего не рисует. Вращение идёт в реальном времени, паузе игры оно не мешает.
using UnityEngine;
using UnityEngine.UI;

public class PetLiveView : MonoBehaviour
{
    const int TexSize = 384;

    public float spinSpeed = 28f;          // градусов в секунду
    public RectTransform rect { get; private set; }

    RawImage img;
    RenderTexture rt;
    PetStageRig rig;
    PetData shown;
    int shownStage = -1;
    float yaw = 30f;

    // интерактив: питомца можно покрутить рукой (Drag) и погладить (Hop: он подпрыгивает и на мгновение раскручивается)
    float dragIdle;            // сколько ещё секунд автоповорот молчит после того, как модель отпустили
    float spinBoost;           // добавка к скорости вращения после Hop, быстро затухает
    float hopT = -1f;          // ход прыжка 0..1 (-1: не прыгает)
    float hopHeight;           // высота прыжка в единицах мира (от размера кадра)
    Vector3 petBase;

    public static PetLiveView Create(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var v = go.AddComponent<PetLiveView>();
        v.rect = (RectTransform)go.transform;
        v.img = go.GetComponent<RawImage>();
        v.img.raycastTarget = false;
        v.img.enabled = false;
        return v;
    }

    // Показать питомца. true = модель показывается, false = нет префаба (карточка тогда рисует иконку).
    public bool Show(PetData d, int stage = 2)
    {
        stage = Mathf.Clamp(stage, 0, 2);
        if (d != null && d == shown && stage == shownStage && rig != null) return true;

        Clear();
        shown = d;
        shownStage = stage;
        if (d == null) return false;

        if (rt == null) rt = PetStage.MakeRT(TexSize);
        rig = PetStage.Build(d, rt, stage);
        if (rig == null)
        {
            img.enabled = false;
            return false;
        }

        rig.cam.enabled = true;
        rig.SetActive(isActiveAndEnabled);     // в закрытом окне студия не работает
        img.texture = rt;
        img.enabled = true;
        yaw = 30f;
        petBase = rig.pet.position;
        hopHeight = Vector3.Distance(rig.cam.transform.position, petBase) * 0.07f;
        hopT = -1f;
        dragIdle = 0f;
        spinBoost = 0f;
        return true;
    }

    // Погладили: подпрыгнуть дважды и чуть раскрутиться
    public void Hop()
    {
        hopT = 0f;
        spinBoost = 360f;
    }

    // Крутят рукой: сдвиг в градусах. Пока крутят (и 1.5 с после) автоповорот молчит
    public void Drag(float degrees)
    {
        yaw += degrees;
        dragIdle = 1.5f;
        spinBoost = 0f;
    }

    void OnEnable()
    {
        if (rig != null) rig.SetActive(true);
    }

    void OnDisable()
    {
        if (rig != null) rig.SetActive(false);
    }

    void Update()
    {
        if (rig == null || rig.pet == null) return;
        float dt = Time.unscaledDeltaTime;
        if (dragIdle > 0f) dragIdle -= dt;
        else yaw += (spinSpeed + spinBoost) * dt;
        spinBoost = Mathf.MoveTowards(spinBoost, 0f, 500f * dt);
        rig.pet.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (hopT >= 0f)
        {
            hopT += dt / 0.6f;
            if (hopT >= 1f)
            {
                hopT = -1f;
                rig.pet.position = petBase;
            }
            else
            {
                float h = hopHeight * Mathf.Abs(Mathf.Sin(hopT * Mathf.PI * 2f)) * (1f - 0.5f * hopT);   // два прыжка, второй ниже
                rig.pet.position = petBase + Vector3.up * h;
            }
        }
    }

    void Clear()
    {
        if (rig != null) rig.Destroy();
        rig = null;
        shown = null;
        shownStage = -1;
    }

    void OnDestroy()
    {
        Clear();
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }
}
