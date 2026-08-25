using System;
using System.Collections.Generic;
using System.IO;
using GCodeGenerator.Models;
using GCodeGenerator.Preview;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Пересборка сцены предпросмотра объединяется в одно обновление.
    ///
    /// Операция сообщает о каждом параметре по отдельности, поэтому сохранение
    /// диалога — это два десятка уведомлений подряд, а загрузка проекта —
    /// ещё и по одному на каждую добавленную операцию. Без объединения сцена
    /// собиралась бы заново на каждое из них, и тем дольше, чем больше проект.
    /// </summary>
    [TestClass]
    public class ScenePreviewBatchingTests
    {
        private static DrillPointsOperation Drill(double x)
            => new DrillPointsOperation
            {
                Holes = { new DrillHole { X = x, Y = 0, TotalDepth = 2, StepDepth = 1 } }
            };

        /// <summary>Считает, сколько раз пересобиралась сцена предпросмотра.</summary>
        private static int CountRebuilds(MainViewModel main, Action action)
        {
            var rebuilds = 0;
            var preview = main.OperationsWorkspace.OperationsPreview;
            OperationScene previous = preview.Scene;

            void OnChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName != nameof(preview.Scene))
                    return;
                if (!ReferenceEquals(preview.Scene, previous))
                    rebuilds++;
                previous = preview.Scene;
            }

            preview.PropertyChanged += OnChanged;
            try
            {
                action();
            }
            finally
            {
                preview.PropertyChanged -= OnChanged;
            }

            return rebuilds;
        }

        [TestMethod]
        public void EditingManyParametersAtOnce_RebuildsSceneOnce()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var operation = new PocketCircleOperation();
            main.OperationsWorkspace.AllOperations.Add(operation);

            var rebuilds = CountRebuilds(main, () =>
            {
                using (main.OperationsWorkspace.BeginBatchUpdate())
                {
                    operation.Radius = 15;
                    operation.TotalDepth = 5;
                    operation.StepDepth = 0.5;
                    operation.FeedXYWork = 250;
                    operation.ToolDiameter = 4;
                }
            });

            Assert.AreEqual(1, rebuilds, "Пять параметров — одно обновление предпросмотра");
        }

        [TestMethod]
        public void WithoutBatch_EveryParameterRebuildsScene()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var operation = new PocketCircleOperation();
            main.OperationsWorkspace.AllOperations.Add(operation);

            var rebuilds = CountRebuilds(main, () =>
            {
                operation.Radius = 15;
                operation.TotalDepth = 5;
            });

            Assert.AreEqual(2, rebuilds, "Без пакета каждое изменение обновляет предпросмотр немедленно");
        }

        /// <summary>
        /// Открытие проекта — одно обновление предпросмотра, сколько бы
        /// операций в файле ни было.
        /// </summary>
        [TestMethod]
        public void OpeningProject_RebuildsSceneOnce()
        {
            var path = Path.Combine(Path.GetTempPath(), $"gcodegen_batch_{Guid.NewGuid():N}.ygc");
            try
            {
                var operations = new List<OperationBase> { Drill(1), Drill(2), Drill(3), Drill(4), Drill(5) };
                new GCodeGenerator.Persistence.ProjectFileService().Save(path, operations, new GCodeSettings());

                var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
                dialogs.OpenDialogResult = path;

                var rebuilds = CountRebuilds(main, () => main.ProjectWorkflow.OpenProjectCommand.Execute(null));

                Assert.AreEqual(5, main.OperationsWorkspace.AllOperations.Count, "Проект открыт целиком");
                Assert.AreEqual(1, rebuilds, "Пять операций — одно обновление предпросмотра");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        /// <summary>
        /// Вложенные пакеты закрываются вместе с внешним: обновление приходит
        /// один раз, когда изменения действительно закончились.
        /// </summary>
        [TestMethod]
        public void NestedBatches_RebuildOnceAtTheEnd()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var operation = new PocketCircleOperation();
            main.OperationsWorkspace.AllOperations.Add(operation);

            var rebuilds = CountRebuilds(main, () =>
            {
                using (main.OperationsWorkspace.BeginBatchUpdate())
                {
                    operation.Radius = 11;
                    using (main.OperationsWorkspace.BeginBatchUpdate())
                        operation.TotalDepth = 6;
                    operation.StepDepth = 2;
                }
            });

            Assert.AreEqual(1, rebuilds, "Вложенный пакет не обновляет предпросмотр раньше времени");
        }

        [TestMethod]
        public void BatchWithoutChanges_DoesNotRebuild()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(new PocketCircleOperation());

            var rebuilds = CountRebuilds(main, () =>
            {
                using (main.OperationsWorkspace.BeginBatchUpdate())
                {
                    // Ничего не меняется.
                }
            });

            Assert.AreEqual(0, rebuilds, "Пустой пакет предпросмотр не трогает");
        }
    }
}
