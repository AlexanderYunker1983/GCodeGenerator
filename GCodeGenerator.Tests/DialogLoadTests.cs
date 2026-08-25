using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Загрузка окон приложения.
    ///
    /// Компиляция разметки проверяет только синтаксис: ссылка на несуществующий
    /// ресурс, неизвестное свойство вложенного блока или неверный тип
    /// присоединённого свойства обнаруживаются лишь при создании окна. Поэтому
    /// каждое окно здесь создаётся по-настоящему — с ресурсами приложения,
    /// но без показа на экране.
    /// </summary>
    [TestClass]
    [SupportedOSPlatform("windows")]
    public class DialogLoadTests
    {
        [TestMethod]
        public void EveryWindow_IsCreatedWithApplicationResources()
        {
            var problems = new List<string>();
            var created = 0;

            RunOnUiThread(() =>
            {
                EnsureApplication();

                foreach (var windowType in WindowTypes())
                {
                    try
                    {
                        var window = (Window)Activator.CreateInstance(windowType);
                        window.Close();
                        created++;
                    }
                    catch (Exception exception)
                    {
                        var reason = exception.InnerException?.Message ?? exception.Message;
                        problems.Add($"{windowType.Name}: {reason}");
                    }
                }
            });

            Assert.IsTrue(created > 0, "Ни одного окна не создано");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        /// <summary>Все окна приложения.</summary>
        private static IEnumerable<Type> WindowTypes()
            => typeof(MainViewModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(Window).IsAssignableFrom(t))
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name, StringComparer.Ordinal);

        /// <summary>
        /// Приложение с загруженными словарями ресурсов: без него разметка не
        /// найдёт ни стилей темы, ни преобразователей. Запуск (<c>Run</c>) не
        /// нужен — окна создаются, но не показываются.
        /// </summary>
        private static void EnsureApplication()
        {
            if (Application.Current != null)
                return;

            var app = new GCodeGenerator.App();
            app.InitializeComponent();
        }

        /// <summary>
        /// Выполняет действие в потоке с однопоточной моделью: окна WPF
        /// создаются только там.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static void RunOnUiThread(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
                throw new InvalidOperationException("Не удалось создать окна приложения", failure);
        }
    }
}
