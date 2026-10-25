// Assets/_Project/Scripts/Workers/WorkerCampSign.cs (НЕ в Editor)
// Табличка с остатками склада лагеря над палаткой (как «Пила: …» над пилорамой). Дизайн «Система рабочих», задача 5.
// Вешать не нужно: WorkerCamp добавляет её сам. Показывает «Склад: бревна 12/30 · доски 3/10», когда рядом игрок,
// лагерь открыт, в нём есть рабочие или остатки, и окно «Лагерь» закрыто. Метка стоит на месте (WorldTag без покачивания).
// Если склад бревен или досок полон, это написано словами («полон»), а не только цветом.
// Вид Candy (облачко с хвостиком, индиго-текст) даёт сам WorldTag; здесь только иконка Candy.
using UnityEngine;

[RequireComponent(typeof(WorkerCamp))]
public class WorkerCampSign : MonoBehaviour
{
    [Tooltip("Высота метки над палаткой (подсказка «E» висит ниже, на promptOffset лагеря)")]
    public Vector3 offset = new Vector3(0f, 3.6f, 0f);
    [Tooltip("С какого расстояния видна табличка (м)")]
    public float showDistance = 16f;

    WorkerCamp camp;
    WorldTag tag;
    GameObject tagGo;
    string shown;
    float nextAt;

    void Awake() { camp = GetComponent<WorkerCamp>(); }

    void Update()
    {
        if (Time.unscaledTime < nextAt) return;
        nextAt = Time.unscaledTime + 0.25f;

        var m = WorkerManager.Instance;
        bool want = camp != null && m != null && camp.Unlocked
                    && (m.WorkingCount > 0 || m.Logs > 0 || m.Planks > 0)
                    && !WorkersWindow.IsOpenFor(camp)
                    && camp.DistanceToPlayer() <= showDistance;

        if (!want)
        {
            if (tag != null) tag.visible = false;
            return;
        }

        if (tag == null)
        {
            tagGo = new GameObject("CampStockSign");
            tagGo.transform.SetParent(transform, false);
            tag = WorldTag.Add(tagGo, false);
            tag.offset = offset;
            tag.SetIcon(CandyTag.Icon(UiIcon.Log));
            tag.SetFontSize(30f);
        }
        tag.visible = true;

        string s = "Склад: бревна " + m.Logs + "/" + m.LogCap + (m.Logs >= m.LogCap ? " (полон)" : "")
                 + " · доски " + m.Planks + "/" + m.PlankCap + (m.Planks >= m.PlankCap ? " (полон)" : "");
        if (s != shown)
        {
            shown = s;
            tag.SetText(s);
        }
    }

    void OnDisable() { if (tag != null) tag.visible = false; }

    void OnDestroy() { if (tagGo != null) Destroy(tagGo); }
}
