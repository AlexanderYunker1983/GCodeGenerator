using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Приложение WPF для тестов, которым нужны настоящие окна.
    ///
    /// Окна и ресурсы приложения принадлежат тому потоку, где созданы, и
    /// обращение к ним из другого потока запрещено платформой. Поэтому поток
    /// здесь один на весь прогон: он создаётся при первом обращении и живёт
    /// до конца, а проверки выполняются на нём по очереди.
    ///
    /// Заодно снят режим завершения по умолчанию: закрытие последнего окна
    /// завершало приложение, и следующая проверка раскладывала окна на
    /// завершённом — с другим результатом, зависящим от порядка тестов.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class TestApplication
    {
        private static readonly object Sync = new object();
        private static Dispatcher _dispatcher;

        /// <summary>
        /// Выполняет действие в потоке окон и ждёт его завершения. Ошибка
        /// внутри действия переносится сюда как есть.
        /// </summary>
        /// <param name="action">Что сделать с окнами.</param>
        public static void Run(Action action)
        {
            var dispatcher = EnsureDispatcher();

            Exception failure = null;
            dispatcher.Invoke(() =>
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

            if (failure != null)
                throw new InvalidOperationException("Проверка окон не удалась", failure);
        }

        private static Dispatcher EnsureDispatcher()
        {
            lock (Sync)
            {
                if (_dispatcher != null)
                    return _dispatcher;

                var ready = new ManualResetEventSlim();
                var thread = new Thread(() =>
                {
                    var app = new GCodeGenerator.App();
                    app.InitializeComponent();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    _dispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();

                    // Поток остаётся живым: окна следующих проверок должны
                    // создаваться там же, где ресурсы приложения.
                    Dispatcher.Run();
                })
                {
                    IsBackground = true,
                };

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                ready.Wait();

                return _dispatcher;
            }
        }
    }
}
