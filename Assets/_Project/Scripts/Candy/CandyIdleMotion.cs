// Assets/_Project/Scripts/UI/Candy/CandyIdleMotion.cs (НЕ в Editor)
// Вечное «дыхание» для украшений и иконок: покачивание вверх-вниз, лёгкий наклон, пульсация размера.
// Подходит звёздам, кубкам, облачкам, значкам NEW, стрелкам-подсказкам. На самих кнопках используйте
// «Привлечь внимание» в CandyButtonJuice (оба скрипта на одном объекте пружинят масштаб и мешают друг другу).
// Не хранит «базовую позу»: добавляет и убирает свою добавку, поэтому дружит с Layout Group.
using UnityEngine;

[DisallowMultipleComponent]
public class CandyIdleMotion : MonoBehaviour
{
    public RectTransform target;
    public float bobHeight = 6f;
    public float bobSpeed = 2.2f;
    public float wobbleAngle = 0f;
    public float wobbleSpeed = 2f;
    [Range(0f, 0.3f)] public float pulse = 0.05f;
    public float pulseSpeed = 2.4f;
    public bool randomPhase = true;

    Vector3 lastOff;
    float lastRot;
    float lastMul = 1f;
    float phase;

    Transform T { get { return target != null ? target : transform; } }

    void OnEnable()
    {
        phase = randomPhase ? Random.value * 6.28f : 0f;
        lastOff = Vector3.zero;
        lastRot = 0f;
        lastMul = 1f;
    }

    void OnDisable()
    {
        var tr = T;
        tr.localPosition -= lastOff;
        tr.localRotation = tr.localRotation * Quaternion.Euler(0f, 0f, -lastRot);
        tr.localScale /= lastMul;
        lastOff = Vector3.zero;
        lastRot = 0f;
        lastMul = 1f;
    }

    void LateUpdate()
    {
        float t = Time.unscaledTime + phase;
        Vector3 off = new Vector3(0f, Mathf.Sin(t * bobSpeed) * bobHeight, 0f);
        float rot = Mathf.Sin(t * wobbleSpeed) * wobbleAngle;
        float mul = 1f + Mathf.Sin(t * pulseSpeed) * pulse;

        var tr = T;
        tr.localPosition += off - lastOff;
        tr.localRotation = tr.localRotation * Quaternion.Euler(0f, 0f, rot - lastRot);
        tr.localScale *= mul / lastMul;

        lastOff = off;
        lastRot = rot;
        lastMul = mul;
    }
}
