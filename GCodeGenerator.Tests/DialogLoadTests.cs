using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GCodeGenerator.Tests.Fixtures;

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

            TestApplication.Run(() =>
            {
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
    }
}
