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

            builder.RegisterAssemblyTypes(typeof(MainViewModel).Assembly)
                .AssignableTo<ViewModelBase>()
                .InstancePerDependency();
        }

        /// <summary>Типы view-моделей всех диалогов операций — из реестра.</summary>
        private static IEnumerable<Type> AllEditorViewModelTypes()
            => OperationEditorRegistry.Registrations.Values
                .Concat(OperationEditorRegistry.DrillRegistrations.Values)
                .Distinct();
    }
}
