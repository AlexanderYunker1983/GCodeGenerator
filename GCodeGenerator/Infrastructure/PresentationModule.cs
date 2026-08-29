#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels;

namespace GCodeGenerator.Infrastructure
{
    /// <summary>
    /// Всё, что относится к интерфейсу: окна, темы, диалоги и view-модели.
    /// </summary>
    public sealed class PresentationModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Сообщения, выбор файла и показ окон — три отдельных контракта:
            // view-модель просит ровно то, чем пользуется.
            builder.RegisterType<WpfMessageService>().As<IMessageService>().SingleInstance();
            builder.RegisterType<WpfFileDialogService>().As<IFileDialogService>().SingleInstance();
            builder.RegisterType<WpfDialogHost>().As<IDialogHost>().SingleInstance();

            // Пункт 7.5 плана: сервис темы через IoC (ранее статика ThemeHelper).
            builder.RegisterType<WpfThemeService>().As<IThemeService>().SingleInstance();

            // Показ файла и открытие ссылки: окну «О программе» нужно
            // и то и другое, а работать с оболочкой само оно не должно.
            builder.RegisterType<ShellService>().As<IShellService>().SingleInstance();

            // Проверка обновлений. Служба существует всегда, спрашивает —
            // только когда её просят: настройка выключена по умолчанию,
            // а кнопка в окне «О программе» — уже действие человека.
            builder.RegisterType<GitHubUpdateService>().As<IUpdateService>().SingleInstance();

            // Сервис создаётся на UI-потоке вместе с первой view-model и
            // запоминает SynchronizationContext для безопасного снимка документа.
            builder.RegisterType<DocumentRecoveryService>()
                .As<IDocumentRecoveryService>()
                .SingleInstance();

            // Пункт 7.3 плана: фабрика диалогов редактора операций (реестр
            // «тип операции → VM диалога»; сверление — по режиму шаблона).
            builder.RegisterType<OperationEditorFactory>()
                .As<IOperationEditorFactory>()
                .SingleInstance();

            // Диалоги операций доступны фабрике по типу view-модели: она
            // получает готовый указатель на них, а не контейнер целиком.
            // Список берётся из того же реестра, по которому диалог выбирается
            // для операции, поэтому третьего перечисления диалогов не возникает.
            foreach (var viewModelType in AllEditorViewModelTypes())
            {
                builder.RegisterType(viewModelType)
                    .Keyed<IOperationEditorViewModel>(viewModelType)
                    .InstancePerDependency();
            }

            builder.RegisterType<GCodeWorkflowFactory>()
                .As<IGCodeWorkflowFactory>()
                .SingleInstance();
            builder.RegisterType<ProjectWorkflowFactory>()
                .As<IProjectWorkflowFactory>()
                .SingleInstance();

            // View-модели, которые собирает контейнер. Рабочие процессы
            // генерации и проекта в этот список не входят: их создают фабрики,
            // передавая коллекцию операций и настройки документа, и публичного
            // конструктора у них нет — по нему они здесь и отсеиваются.
            builder.RegisterAssemblyTypes(typeof(MainViewModel).Assembly)
                .AssignableTo<ViewModelBase>()
                .Where(type => type.GetConstructors().Length > 0)
                .InstancePerDependency();
        }

        /// <summary>Типы view-моделей всех диалогов операций — из реестра.</summary>
        private static IEnumerable<Type> AllEditorViewModelTypes()
            => OperationEditorRegistry.Registrations.Values
                .Concat(OperationEditorRegistry.DrillRegistrations.Values)
                .Distinct();
    }
}
