namespace GCodeGenerator.Localization
{
    /// <summary>
    /// Статический доступ к менеджеру локализации для XAML-привязок (пункт 1.3 плана).
    /// Заполняется при инициализации (composition root) до загрузки окон.
    /// </summary>
    public static class LocalizationProvider
    {
        public static ILocalizationManager Instance { get; set; }
    }
}
