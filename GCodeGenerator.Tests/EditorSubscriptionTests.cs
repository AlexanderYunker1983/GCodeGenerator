using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using GCodeGenerator.ViewModels.Drill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Закрытое окно операции перестаёт её слушать.
    ///
    /// Диалог следит за операцией, пока открыт: от её параметров зависят
    /// видимость полей и предпросмотр расстановки отверстий. Раньше подписку
    /// заводил каждый наследник сам, а снимать её было негде — и это не
    /// оставалось безобидным. Новую операцию диалог правит напрямую, а не
    /// копией, поэтому после подтверждения она уходит в документ вместе с
    /// живой ссылкой на view-модель закрытого окна: та не собиралась сборщиком
    /// мусора и продолжала работать на каждое изменение операции. У сверления
    /// это означало полный пересчёт расстановки — а перенос правок присваивает
    /// все свойства операции подряд, то есть по пересчёту на каждое.
    ///
    /// Теперь подписка живёт в основе редактора: наследники переопределяют
    /// метод, а не подписываются, и снять подписку при закрытии нельзя забыть.
    /// </summary>
    [TestClass]
    public class EditorSubscriptionTests
    {
        /// <summary>Диалог, считающий, сколько раз его позвали.</summary>
        private sealed class CountingEditorVm : OperationEditorViewModelBase<PocketCircleOperation>
        {
            public int Notifications { get; private set; }

            protected override void OnOperationPropertyChanged(
                PocketCircleOperation operation, PropertyChangedEventArgs e) => Notifications++;
        }

        [TestMethod]
        public void OpenEditor_FollowsTheOperation()
        {
            var operation = new PocketCircleOperation { Radius = 10 };
            var editor = new CountingEditorVm { Operation = operation };

            operation.Radius = 20;

            Assert.AreEqual(1, editor.Notifications, "Открытое окно следит за операцией");
        }

        [TestMethod]
        public void ClosedEditor_StopsFollowingTheOperation()
        {
            var operation = new PocketCircleOperation { Radius = 10 };
            var editor = new CountingEditorVm { Operation = operation };

            editor.OnClosed();
            operation.Radius = 20;

            Assert.AreEqual(0, editor.Notifications, "Закрытое окно операцию больше не слушает");
        }

        /// <summary>
        /// Диалог, получивший другую операцию, отпускает прежнюю: иначе он
        /// пересчитывал бы своё состояние по чужим правкам.
        /// </summary>
        [TestMethod]
        public void ReplacedOperation_IsReleased()
        {
            var first = new PocketCircleOperation { Radius = 10 };
            var second = new PocketCircleOperation { Radius = 30 };
            var editor = new CountingEditorVm { Operation = first };

            editor.Operation = second;
            first.Radius = 20;

            Assert.AreEqual(0, editor.Notifications, "Прежняя операция отпущена");

            second.Radius = 40;

            Assert.AreEqual(1, editor.Notifications, "Новая операция слушается");
        }

        /// <summary>
        /// Повторный показ того же диалога с той же операцией восстанавливает
        /// слежение: значение не меняется, и через сеттер это не пройдёт.
        /// </summary>
        [TestMethod]
        public void ReopenedEditor_FollowsTheSameOperationAgain()
        {
            var operation = new PocketCircleOperation { Radius = 10 };
            var editor = new CountingEditorVm();
            var asEditor = (IOperationEditorViewModel)editor;

            asEditor.SetOperation(operation);
            editor.OnClosed();
            asEditor.SetOperation(operation);
            operation.Radius = 20;

            Assert.AreEqual(1, editor.Notifications, "Открытое заново окно снова следит за операцией");
        }

        /// <summary>
        /// Сверление — самый дорогой случай: диалог пересчитывает всю
        /// расстановку на каждый изменённый параметр. Закрытое окно этого
        /// делать не должно.
        /// </summary>
        [TestMethod]
        public void ClosedDrillEditor_DoesNotRebuildHoles()
        {
            var operation = DrillPointsOperation.CreateNew(DrillMode.Line);
            operation.HoleCount = 5;
            var editor = new DrillLineOperationViewModel(null);
            var asEditor = (IOperationEditorViewModel)editor;
            asEditor.SetOperation(operation);

            Assert.AreEqual(5, editor.PreviewHoles.Count, "Открытое окно показывает расстановку");

            editor.OnClosed();
            operation.HoleCount = 9;

            Assert.AreEqual(5, editor.PreviewHoles.Count,
                "Закрытое окно расстановку не пересчитывает");
        }

        /// <summary>
        /// Прямое доказательство: операция, ушедшая в документ, не держит
        /// закрытое окно. Именно так работает создание новой операции —
        /// диалог правит её саму, и она переживает окно.
        /// </summary>
        [TestMethod]
        public void OperationInDocument_DoesNotKeepClosedEditorAlive()
        {
            var operation = DrillPointsOperation.CreateNew(DrillMode.Line);
            var editor = CreateAndCloseEditor(operation);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var editorSurvived = editor.IsAlive;

            // Операция обязана дожить до этого места: она изображает операцию,
            // лежащую в документе. Без этого сборщик вправе собрать её вместе
            // с окном ещё до проверки — тогда проверка проходила бы и при
            // невыгруженной подписке, ничего не доказывая.
            GC.KeepAlive(operation);

            Assert.IsFalse(editorSurvived,
                "Операция документа держит view-модель закрытого окна — подписка не снята");
        }

        /// <summary>
        /// Окно создаётся и закрывается в отдельном методе: иначе ссылка на
        /// него осталась бы в кадре стека вызывающего и держала бы объект
        /// живым независимо от подписки.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateAndCloseEditor(DrillPointsOperation operation)
        {
            var editor = new DrillLineOperationViewModel(null);
            ((IOperationEditorViewModel)editor).SetOperation(operation);
            editor.OnClosed();
            return new WeakReference(editor);
        }

        /// <summary>
        /// Ни один редактор не подписывается на операцию сам: подписка живёт
        /// в основе редактора, и обойти её — значит вернуть утечку в обход
        /// всех проверок выше.
        ///
        /// Проверяются только диалоги операций — файлы, где объявлен наследник
        /// основы редактора. Сама основа исключена: подписка объявлена в ней,
        /// и это единственное её законное место. Рабочая область документа под
        /// правило не подпадает — она следит за операциями по другому поводу
        /// и отписывается от них сама.
        /// </summary>
        [TestMethod]
        public void NoEditor_SubscribesToTheOperationItself()
        {
            var violations = new List<string>();
            var viewModels = System.IO.Path.Combine(
                RepositoryRootLocator.Find(), "GCodeGenerator", "ViewModels");
            var ruleItself = typeof(OperationEditorViewModelBase<OperationBase>).Name.Split('`')[0] + ".cs";

            foreach (var path in System.IO.Directory.EnumerateFiles(
                         viewModels, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                if (string.Equals(System.IO.Path.GetFileName(path), ruleItself, StringComparison.Ordinal))
                    continue;

                var text = System.IO.File.ReadAllText(path);
                if (!text.Contains(": OperationEditorViewModelBase<", StringComparison.Ordinal)
                    && !text.Contains("EditorViewModelBase<", StringComparison.Ordinal))
                {
                    continue;
                }

                var lines = System.IO.File.ReadAllLines(path);
                for (var index = 0; index < lines.Length; index++)
                {
                    var line = lines[index];
                    if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                        continue;

                    if (line.Contains(".PropertyChanged +=", StringComparison.Ordinal))
                        violations.Add($"{System.IO.Path.GetFileName(path)}:{index + 1}: {line.Trim()}");
                }
            }

            Assert.AreEqual(0, violations.Count,
                "Переопределите OnOperationPropertyChanged вместо собственной подписки:"
                + Environment.NewLine + string.Join(Environment.NewLine, violations));
        }
    }
}
