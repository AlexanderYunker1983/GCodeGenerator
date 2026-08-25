using Autofac;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Import;
using GCodeGenerator.Persistence;
using GCodeGenerator.Services;

namespace GCodeGenerator.Infrastructure
{
    /// <summary>
    /// Службы, не зависящие от интерфейса: генерация программы, файлы проекта
    /// и G-кода, импорт чертежей, пользовательские настройки.
    ///
    /// Раньше все регистрации лежали одним списком в методе запуска, и найти
    /// среди них нужную можно было только чтением сверху вниз.
    /// </summary>
    public sealed class CoreServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Хранилище пользовательских настроек принадлежит IoC-контейнеру;
            // статический compatibility-фасад удалён после переходного релиза.
            builder.RegisterType<AppSettingsStore>().As<ISettingsStore>().SingleInstance();

            // Пункт 7.6 плана: служба файлов проекта через IoC (new из VM удалён).
            builder.RegisterType<ProjectFileService>().As<IProjectFileService>().SingleInstance();
            builder.RegisterType<GCodeFileService>().As<IGCodeFileService>().SingleInstance();

            // DXF-парсинг и геометрическое восстановление контуров не являются
            // обязанностью диалоговых ViewModel и доступны через отдельный сервис.
            builder.RegisterType<DxfImportService>().As<IDxfImportService>().SingleInstance();

            // Пункт 4.5 плана: явный маппинг «тип операции → генератор»
            // (name-based рефлексия удалена).
            builder.RegisterType<OperationGeneratorRegistry>()
                .As<IOperationGeneratorRegistry>()
                .SingleInstance();
            builder.RegisterType<SimpleGCodeGenerator>()
                .As<IGCodeGenerator>()
                .SingleInstance();

            // Всё, что зависит от станка, а не от детали: модальные состояния
            // в начале программы, вид команд шпинделя и охлаждения, единица
            // аргумента паузы, завершение программы.
            builder.RegisterType<GenericPostProcessor>()
                .As<IPostProcessor>()
                .SingleInstance();
        }
    }
}
