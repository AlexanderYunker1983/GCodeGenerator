using System;
using System.Globalization;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels.Pocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Числовые поля ввода.
    ///
    /// Культура привязок WPF по умолчанию — «en-US», а не культура
    /// пользователя, поэтому набранная запятая читалась как разделитель
    /// разрядов: «1,5» превращалось в 15 — глубина прохода в десять раз
    /// больше запрошенной, молча и без следа. Точку поле тоже не принимало,
    /// но по другой причине: «0.» — законное число 0, поэтому привязка
    /// обновляла значение и переписывала текст обратно, стирая только что
    /// набранный разделитель. Буквы при этом набирались свободно и оставались
    /// на экране, хотя в операцию не попадали.
    ///
    /// Тесты воспроизводят набор посимвольно — так же, как это делает
    /// пользователь.
    /// </summary>
    [TestClass]
    [SupportedOSPlatform("windows")]
    public class NumericInputTests
    {
        /// <summary>
        /// Набирает текст в поле по одному символу, отбрасывая символы,
        /// которые поле не принимает, — как при вводе с клавиатуры.
        /// </summary>
        private static (string Text, double Value) Type(string keys, double initialValue = 1.0)
        {
            string text = null;
            double value = 0;

            RunOnUiThread(() =>
            {
                var vm = new PocketRectangleOperationViewModel(null)
                {
                    Operation = new PocketRectangleOperation()
                };
                vm.StepDepth = initialValue;

                var box = new TextBox { DataContext = vm };
                NumericInput.SetMode(box, NumericInputMode.Decimal);
                box.SetBinding(TextBox.TextProperty, new Binding("StepDepth")
                {
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    Converter = new NumericTextConverter()
                });

                box.Text = string.Empty;
                foreach (var key in keys)
                {
                    var candidate = box.Text + key;
                    if (!NumericInput.IsAllowed(box, candidate))
                        continue; // поле не пропускает символ
                    box.Text = candidate;
                }

                text = box.Text;
                value = vm.StepDepth;
            });

            return (text, value);
        }

        [TestMethod]
        public void Dot_IsAcceptedAndKept()
        {
            var (text, value) = Type("0.5");

            Assert.AreEqual("0.5", text, "Набранная точка остаётся в поле");
            Assert.AreEqual(0.5, value, 1e-9, "Значение операции — ноль целых пять десятых");
        }

        /// <summary>
        /// Главная защита: запятая означает дробную часть, а не разряды.
        /// Прежде «1,5» давало 15 — фрезу, уходящую на порядок глубже.
        ///
        /// Набранная запятая заменяется точкой, как только число сложилось:
        /// поле показывает ровно тот вид, в котором значение уйдёт в G-code.
        /// </summary>
        [TestMethod]
        public void Comma_MeansFraction_NotThousands()
        {
            var (text, value) = Type("1,5");

            Assert.AreEqual(1.5, value, 1e-9, "Запятая — дробная часть, а не разделитель разрядов");
            Assert.AreEqual("1.5", text, "Поле показывает разделитель так же, как G-code");
        }

        [TestMethod]
        public void Letters_AreNotAccepted()
        {
            var (text, value) = Type("12abc3");

            Assert.AreEqual("123", text, "Буквы в поле не попадают");
            Assert.AreEqual(123, value, 1e-9, "В операцию уходит только набранное число");
        }

        [TestMethod]
        public void SecondSeparator_IsNotAccepted()
        {
            var (text, _) = Type("1.2.3");

            Assert.AreEqual("1.23", text, "Второй разделитель отброшен");
        }

        [TestMethod]
        public void NegativeValue_IsAccepted()
        {
            var (text, value) = Type("-2.5");

            Assert.AreEqual("-2.5", text, "Минус в начале допустим: высоты бывают отрицательными");
            Assert.AreEqual(-2.5, value, 1e-9, "Отрицательное значение доходит до операции");
        }

        /// <summary>
        /// Незавершённый ввод не должен ронять значение: пока набрано «0.»,
        /// в операции остаётся то, что было до начала набора.
        /// </summary>
        [TestMethod]
        public void PartialInput_KeepsPreviousValue()
        {
            var (text, value) = Type("0.", initialValue: 3.0);

            Assert.AreEqual("0.", text, "Разделитель остаётся на экране");
            Assert.AreEqual(0, value, 1e-9, "Значение соответствует уже набранной части");
        }

        /// <summary>
        /// Фильтр должен быть именно подключён к полю: проверка через событие
        /// ввода, а не через вызов функции — иначе легко получить правильную
        /// логику, которую никто не вызывает.
        /// </summary>
        [TestMethod]
        public void InputFilter_IsWiredToTheTextBox()
        {
            bool letterBlocked = false;
            bool digitAllowed = false;

            RunOnUiThread(() =>
            {
                var box = new TextBox();
                NumericInput.SetMode(box, NumericInputMode.Decimal);

                letterBlocked = RaiseTextInput(box, "a");
                digitAllowed = !RaiseTextInput(box, "7");
            });

            Assert.IsTrue(letterBlocked, "Буква до поля не доходит");
            Assert.IsTrue(digitAllowed, "Цифра вводится обычным образом");
        }

        /// <summary>Поднимает событие ввода символа; true — ввод отклонён.</summary>
        [SupportedOSPlatform("windows")]
        private static bool RaiseTextInput(TextBox box, string text)
        {
            var composition = new System.Windows.Input.TextComposition(
                System.Windows.Input.InputManager.Current, box, text);
            var args = new System.Windows.Input.TextCompositionEventArgs(
                System.Windows.Input.InputManager.Current.PrimaryKeyboardDevice, composition)
            {
                RoutedEvent = System.Windows.Input.TextCompositionManager.PreviewTextInputEvent
            };

            box.RaiseEvent(args);
            return args.Handled;
        }

        [TestMethod]
        public void IntegerField_RejectsSeparator()
        {
            Assert.IsTrue(NumericInput.IsAllowed(NumericInputMode.Integer, "12"), "Цифры допустимы");
            Assert.IsTrue(NumericInput.IsAllowed(NumericInputMode.Integer, "-3"), "Минус допустим");
            Assert.IsFalse(NumericInput.IsAllowed(NumericInputMode.Integer, "1.5"), "Дробь в целом поле недопустима");
            Assert.IsFalse(NumericInput.IsAllowed(NumericInputMode.Integer, "1a"), "Буква недопустима");
        }

        /// <summary>
        /// Значение показывается с точкой независимо от культуры системы —
        /// в G-code разделитель всегда точка, и поле показывает то же самое.
        /// </summary>
        [TestMethod]
        public void DisplayedValue_UsesDotSeparator()
        {
            var converter = new NumericTextConverter();
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Assert.AreEqual("1.5", converter.Convert(1.5, typeof(string), null, CultureInfo.CurrentCulture));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [TestMethod]
        public void EmptyInput_DoesNotChangeValue()
        {
            var converter = new NumericTextConverter();

            Assert.AreSame(Binding.DoNothing, converter.ConvertBack("", typeof(double), null, CultureInfo.InvariantCulture),
                "Пустое поле не обнуляет параметр операции");
            Assert.AreSame(Binding.DoNothing, converter.ConvertBack("-", typeof(double), null, CultureInfo.InvariantCulture),
                "Один минус — начало набора, не значение");
        }

        /// <summary>
        /// Каждое числовое поле разметки должно объявлять режим ввода:
        /// забытое поле снова принимало бы буквы и путало разделители.
        /// </summary>
        [TestMethod]
        public void EveryNumericBinding_DeclaresInputMode()
        {
            var viewsDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "GCodeGenerator", "Views"));
            var problems = new System.Collections.Generic.List<string>();
            var checkedBindings = 0;

            foreach (var file in System.IO.Directory.GetFiles(viewsDirectory, "*.xaml", System.IO.SearchOption.AllDirectories))
            {
                foreach (var line in System.IO.File.ReadAllLines(file))
                {
                    var isTextBox = line.Contains("<TextBox") && line.Contains("UpdateSourceTrigger=PropertyChanged");
                    // Колонка таблицы отверстий редактируется тем же полем ввода.
                    var isEditableColumn = line.Contains("DataGridTextColumn")
                        && line.Contains("UpdateSourceTrigger=PropertyChanged");
                    if (!isTextBox && !isEditableColumn)
                        continue;

                    checkedBindings++;
                    var declaresMode = line.Contains("NumericInput.Mode=")
                        || line.Contains("EditingElementStyle=\"{StaticResource NumericCellEditor}\"");
                    if (!declaresMode)
                        problems.Add($"{System.IO.Path.GetFileName(file)}: {line.Trim()}");
                    else if (!line.Contains("Converter={StaticResource NumericText}"))
                        problems.Add($"{System.IO.Path.GetFileName(file)} (нет преобразователя): {line.Trim()}");
                }
            }

            Assert.IsTrue(checkedBindings > 100, $"Проверено полей: {checkedBindings}");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        [SupportedOSPlatform("windows")]
        private static void RunOnUiThread(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception exception) { failure = exception; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
                throw new InvalidOperationException("Проверка поля ввода не выполнена", failure);
        }
    }
}
