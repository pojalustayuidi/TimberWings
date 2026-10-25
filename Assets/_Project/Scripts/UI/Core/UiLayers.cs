// Assets/_Project/Scripts/UI/Core/UiLayers.cs (НЕ в Editor)
// Единое место для sortingOrder всех канвасов. Раньше числа были разбросаны по 30 файлам и подбирались
// комментариями вида «выше окна прокачки (30)». Теперь порядок слоёв виден целиком и меняется в одной строке.
// Использование: canvas.sortingOrder = UiLayers.Window;
public static class UiLayers
{
    // мир и подсказки
    public const int WorldTag        = 5;     // WorldTag, BuildSiteTagView, PlayerHeadHud
    public const int QuestEdgeArrow  = 6;
    public const int QuestGuide      = 7;
    // HUD
    public const int Hud             = 10;    // IntroFlow и основной HUD
    public const int Dialogue        = 11;
    public const int HudOverlay      = 12;    // миникарта, мобильное управление, мини-панели коллекций
    public const int Popups          = 20;    // RewardPopups, QuestCelebration
    // окна
    public const int BuildReveal     = 28;
    public const int WindowLow       = 29;    // стройка, продажа, пилорама
    public const int WindowUpgrade   = 30;
    public const int Window          = 31;    // клеть, коллекция питомцев, достижения, рабочие
    public const int Toast           = 45;    // выше всех окон
    public const int Ending          = 100;
    public const int Showcase        = 200;   // только отладочный UiKitShowcase
    public const int OrbitCameraHud  = 400;
    // системное
    public const int Settings        = 1000;  // выше любых канвасов сцены
    public const int MainMenu        = 5000;
    public const int Fade            = 29000; // CampSleep, IslandEdgeGuard («вы упали»)
    public const int SaveIndicator   = 30000; // всегда поверх
}
