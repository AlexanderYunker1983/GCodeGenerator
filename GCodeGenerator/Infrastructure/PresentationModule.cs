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
            builder.RegisterType<WpfDialogService>().As<IDialogService>().SingleInstance();

            // Пункт 7.5 плана: сервис темы через IoC (ранее статика ThemeHelper).
            builder.RegisterType<WpfThemeService>().As<IThemeService>().SingleInstance();

            // Пункт 7.3 плана: фабрика диалогов редактора операций (реестр
            // «тип операции → VM диалога»; сверление — по режиму шаблона).
            builder.RegisterType<OperationEditorFactory>()
                .As<IOperationEditorFactory>()
                .SingleInstance();

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
    }
}
