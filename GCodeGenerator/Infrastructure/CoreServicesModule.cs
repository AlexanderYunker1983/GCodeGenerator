#nullable enable
using Autofac;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.GCodeGenerators.Strategies;
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

            // Реестр стратегий выборки кармана: экземпляр с интерфейсом,
            // чтобы способ выборки можно было расширить через контейнер,
            // не меняя генератор.
            builder.RegisterType<PocketStrategyRegistry>()
                .As<IPocketStrategyRegistry>()
                .SingleInstance();

            // Пункт 4.5 плана: явный маппинг «тип операции → генератор»
            // (name-based рефлексия удалена). Генератор карманов получает
            // реестр стратегий из контейнера.
            builder.Register(c => new OperationGeneratorRegistry(
                    new DrillPointsOperationGenerator(),
                    new UnifiedProfileGenerator(),
                    new UnifiedPocketGenerator(c.Resolve<IPocketStrategyRegistry>())))
                .As<IOperationGeneratorRegistry>()
                .SingleInstance();
            builder.RegisterType<SimpleGCodeGenerator>()
                .As<IGCodeGenerator>()
                .SingleInstance();

            // Всё, что зависит от станка, а не от детали: модальные состояния
            // в начале программы, вид команд шпинделя и охлаждения, единица
            // аргумента паузы, завершение программы. Стойка выбирается
            // настройкой Format.PostProcessorName из реестра.
            //
            // Каждая стойка регистрируется сама: реестр принимает их
            // коллекцией, и новая стойка добавляется одной строкой здесь.
            // Регистрации обязательны: Autofac выбирает конструктор
            // с наибольшим числом разрешимых параметров, а коллекцию
            // интерфейса он умеет собирать и пустой — реестр без этих
            // строк собрался бы БЕЗ ЕДИНОЙ СТОЙКИ, и генерация отказывала
            // бы любым настройкам. Пустой набор реестр отвергает сам,
            // а полноту набора из контейнера держит тест.
            builder.RegisterType<GenericPostProcessor>().As<IPostProcessor>().SingleInstance();
            builder.RegisterType<GrblPostProcessor>().As<IPostProcessor>().SingleInstance();
            builder.RegisterType<PostProcessorRegistry>()
                .As<IPostProcessorRegistry>()
                .SingleInstance();
        }
    }
}
