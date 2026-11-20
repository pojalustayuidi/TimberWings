// Shared Russian/English preference for menus, Chapter I objectives, and dialogue playback.
using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameLanguage
{
    const string PrefKey = "tw_ui_language";
    public static event Action Changed;

    public static int Index
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 1); }
        set
        {
            int next = Mathf.Clamp(value, 0, 1);
            if (Index == next) return;
            PlayerPrefs.SetInt(PrefKey, next);
            PlayerPrefs.Save();
            var handler = Changed;
            if (handler != null) handler();
        }
    }

    public static bool English { get { return Index == 1; } }

    static readonly Dictionary<string, string> EnglishText = new Dictionary<string, string>
    {
        { "Интерфейс", "Interface" },
        { "Оформление настроек", "Settings appearance" },
        { "Классическое", "Classic" },
        { "Лесное", "Forest" },
        { "Сохранение по таймеру", "Timed autosave" },
        { "Редкие реплики героя", "Occasional hero comments" },
        { "Язык переводит настройки и главное меню.", "Language sets the menus and Chapter I subtitles and voices." },
        { "Прогресс сохраняется и при важных событиях.", "Important game events are saved independently." },
        { "FPS в настройках", "FPS in Settings" },
        { "Анимация открытия окна", "Animate window opening" },
        { "Размер окна настроек", "Settings window size" },
        { "Затемнение за меню", "Menu backdrop dimming" },
        { "Масштаб влияет только на окно настроек.", "Scaling only affects the Settings window." },
        { "Быстрые советы", "Quick tips" },
        { "Закрыть окно без применения изменений?", "Leave without applying changes?" },
        { "Настройки", "Settings" },
        { "Общие", "General" },
        { "Графика", "Graphics" },
        { "Звук", "Audio" },
        { "Управление", "Controls" },
        { "Игра", "Game" },
        { "Язык", "Language" },
        { "Русский", "Russian" },
        { "English", "English" },
        { "Подсказки к цели", "Objective hints" },
        { "Автопереход реплик", "Auto-advance dialogue" },
        { "Показывать FPS", "Show FPS in settings" },
        { "Меньше тряски и эффектов", "Reduce effects" },
        { "Редкие реплики персонажей", "Character comments" },
        { "Выбор языка изменяет настройки и главное меню.", "Language sets the menus and Chapter I subtitles and voices." },
        { "Качество света", "Lighting quality" },
        { "Низкое", "Low" },
        { "Среднее", "Medium" },
        { "Высокое", "High" },
        { "Дальность прорисовки", "View distance" },
        { "Ближняя", "Near" },
        { "Средняя", "Medium" },
        { "Дальняя", "Far" },
        { "Тени", "Shadows" },
        { "Дымка вдали", "Distance fog" },
        { "Лимит FPS", "FPS limit" },
        { "Масштаб рендера", "Render scale" },
        { "Полноэкранный режим", "Fullscreen" },
        { "Звук в игре", "Game audio" },
        { "Общая громкость", "Master volume" },
        { "Музыка", "Music" },
        { "Звуковые эффекты", "Sound effects" },
        { "Голоса", "Voices" },
        { "Звуки природы", "Ambience" },
        { "Речь персонажей", "Character speech" },
        { "Озвучка", "Recorded voices" },
        { "Бормотание", "Babble" },
        { "Выкл", "Off" },
        { "Вкл", "On" },
        { "Проверка текущего голоса", "Voice preview" },
        { "Прослушать", "Preview" },
        { "Режим камеры", "Camera mode" },
        { "Свободная", "Free orbit" },
        { "Изометрия", "Isometric" },
        { "Поворот мышью без ПКМ", "Mouse look without RMB" },
        { "Чувствительность мыши", "Mouse sensitivity" },
        { "Покачивание камеры от первого лица", "First-person head bob" },
        { "WASD — движение   ·   Esc — пауза", "WASD — move   ·   Esc — pause" },
        { "E / ЛКМ — взаимодействие и рубка", "E / LMB — interact and chop" },
        { "Колесо — зум   ·   Alt — свободный курсор", "Wheel — zoom   ·   Alt — free cursor" },
        { "Экранные кнопки (после перезапуска)", "Touch controls (restart)" },
        { "Скорость суток", "Day/night speed" },
        { "Обычная", "Normal" },
        { "Медленная", "Slow" },
        { "Фиксированный день", "Permanent daytime" },
        { "Сохранение прогресса", "Save progress" },
        { "Сохранить", "Save" },
        { "Загрузить сохранение", "Load save" },
        { "Продолжить", "Continue" },
        { "Сброс прогресса", "Reset progress" },
        { "Новая игра", "New game" },
        { "Вернуться с сохранением", "Save and return" },
        { "В главное меню", "Main menu" },
        { "Закрыть игру", "Quit game" },
        { "Выйти", "Exit" },
        { "Управление сохранением доступно из паузы.", "Save controls are available during gameplay." },
        { "По умолчанию", "Restore defaults" },
        { "Применить", "Apply" },
        { "Назад", "Back" },
        { "Изменения вступают в силу после применения.", "Changes take effect after you apply them." },
        { "Есть изменения. Нажми «Применить», чтобы их сохранить.", "Pending changes. Press Apply to save them." },
        { "Значения по умолчанию готовы. Нажми «Применить».", "Defaults selected. Press Apply to save them." },
        { "Настройки применены", "Settings applied" },
        { "НЕСОХРАНЁННЫЕ ИЗМЕНЕНИЯ", "UNSAVED CHANGES" },
        { "Закрыть настройки без применения изменений?", "Leave without applying your changes?" },
        { "Не сохранять", "Discard" },
        { "НОВАЯ ИГРА", "NEW GAME" },
        { "Весь прогресс будет удалён.\nЭто нельзя отменить.", "All progress will be deleted.\nThis cannot be undone." },
        { "Удалить", "Delete" },
        { "Отмена", "Cancel" },
        { "В ГЛАВНОЕ МЕНЮ", "MAIN MENU" },
        { "Игра будет сохранена.\nВернуться в главное меню?", "Your progress will be saved.\nReturn to the main menu?" },
        { "В меню", "Go to menu" },
        { "Выход", "Exit" },
        { "Сохранить прогресс и выйти из игры?", "Save progress and quit?" },
    };

    public static string T(string russian)
    {
        if (!English || string.IsNullOrEmpty(russian)) return russian;
        string translated;
        return EnglishText.TryGetValue(russian, out translated) ? translated : ChapterOneText.TranslateLegacy(russian);
    }
}
