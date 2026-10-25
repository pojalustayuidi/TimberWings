// Assets/_Project/Scripts/Forest/LandmarkTree.cs (НЕ в Editor)
// Достопримечательность-дерево: «Дедушка» (вековое дерево). Срубить нельзя: у объекта нет тега Resource и нет ResourceItem,
// поэтому PlayerInteraction его не считает ресурсом. Зато с ним можно «поздороваться» по E (IInteractable).
// Вешается на корень префаба Landmark_Grandpa (его собирает Editor/IslandV2/Forest/GrandpaTree.cs).
// Если кто-то по ошибке повесит на Дедушку тег Resource или ResourceItem, в Awake они убираются, а в Console пишется предупреждение.
// v2: каждое приветствие показывает другую реплику (по кругу, начиная со случайной) и сыплет горсть лепестков (GrandpaVibe.Burst).
using UnityEngine;

public class LandmarkTree : MonoBehaviour, IInteractable
{
    [Header("Название (тост при приветствии)")]
    public string title = "Дедушка";
    public string caption = "ДРЕВНИЙ ХРАНИТЕЛЬ";
    public Color toastColor = new Color(0.55f, 0.85f, 0.45f);

    [Header("Реплики (подпись тоста). Пусто = всегда caption")]
    public string[] greetings =
    {
        "ЗДРАВСТВУЙ, ДРУГ",
        "ПРИСЯДЬ, ОТДОХНИ",
        "ЛЕС ТЕБЯ ПОМНИТ",
        "Я ВИДЕЛ ЭТОТ ОСТРОВ МОЛОДЫМ",
        "ВЕТЕР ШЕПЧЕТ ТВОЁ ИМЯ",
        "ДОБРЫЙ ДЕНЬ ДЛЯ ПРОГУЛКИ"
    };

    [Header("Взаимодействие")]
    [Tooltip("Подсказка: «Нажми E, чтобы …»")]
    public string prompt = "поздороваться";
    [Tooltip("С какого расстояния (по земле, от оси ствола) доступно E, м")]
    public float radius = 7.5f;
    [Tooltip("Высота над землёй, где рисуется «Нажми E», м")]
    public float promptHeight = 2.4f;

    public static readonly System.Collections.Generic.List<LandmarkTree> All = new System.Collections.Generic.List<LandmarkTree>(4);

    int nextLine = -1;

    void Awake()
    {
        var ri = GetComponent<ResourceItem>();
        if (ri != null)
        {
            Debug.LogWarning("LandmarkTree «" + title + "»: найден ResourceItem, значит дерево можно срубить. Убираю компонент.", this);
            Destroy(ri);
        }
        if (CompareTag("Resource"))
        {
            Debug.LogWarning("LandmarkTree «" + title + "»: стоит тег Resource, дерево можно срубить. Ставлю Untagged.", this);
            tag = "Untagged";
        }
    }

    void OnEnable() { Interactables.Register(this); if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { Interactables.Unregister(this); All.Remove(this); }

    // ------------------------------------------------------------ IInteractable
    public Transform Anchor { get { return transform; } }
    public Vector3 PromptPoint { get { return transform.position + Vector3.up * promptHeight; } }
    public string Prompt { get { return prompt; } }
    public float Radius { get { return radius; } }
    public bool CanInteract { get { return true; } }

    public void Interact()
    {
        string line = caption;
        if (greetings != null && greetings.Length > 0)
        {
            if (nextLine < 0) nextLine = Random.Range(0, greetings.Length);
            line = greetings[nextLine % greetings.Length];
            nextLine = (nextLine + 1) % greetings.Length;
        }
        ZoneToast.Show(title, line, toastColor);
        var vibe = GetComponent<GrandpaVibe>();
        if (vibe != null) vibe.Burst();
        var sacred = GetComponent<SacredVibe>();   // деревья-хранители (ForestSacred.cs): вспышка рун, кристалл, частицы
        if (sacred != null) sacred.Burst();
    }
}
