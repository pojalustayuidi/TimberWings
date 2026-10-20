// Assets/_Project/Scripts/Save/ISaveable.cs (НЕ в Editor)
// Контракт системы, которая хранит прогресс. Регистрация: SaveSystem.Register(this) в Awake, Unregister в OnDestroy.
// Образец: Pets/PetManager.cs. Новые поля внутри секции добавляем только с дефолтами (старые файлы читаются без миграции).
public interface ISaveable
{
    string SaveKey { get; }      // ключ секции в файле (settings, stats, inventory, upgrades, skills, workers, pets, buildings, story, intro, time)
    int LoadOrder { get; }       // порядок загрузки: settings 0, stats 10, inventory 20, upgrades 30, skills 35, workers 40, pets 50, buildings 60, story 70, intro 80, time 90
    string Save();               // состояние в JSON (JsonUtility)
    void Load(string json, int formatVersion);   // во время Load не выдавать награды, события, тосты, диалоги и не звать RequestSave
    void ResetToDefault();       // чистая игра
}
