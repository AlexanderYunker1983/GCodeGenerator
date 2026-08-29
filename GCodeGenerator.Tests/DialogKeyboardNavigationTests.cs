#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public sealed class DialogKeyboardNavigationTests
    {
        private static string ViewsDirectory =>
            Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "GCodeGenerator", "Views"));

        [TestMethod]
        public void DialogCompletionButtons_FollowEditableContentInTabOrder()
        {
            XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var problems = new List<string>();

            foreach (var path in Directory.GetFiles(ViewsDirectory, "*.xaml", SearchOption.AllDirectories))
            {
                var document = XDocument.Load(path);
                foreach (var button in document.Descendants(presentation + "Button")
                             .Where(element => IsTrue(element, "IsDefault") || IsTrue(element, "IsCancel")))
                {
                    var tabIndex = (int?)button.Attribute("TabIndex");
                    if (tabIndex is null or < 1000)
                        problems.Add($"{Path.GetFileName(path)}: кнопка завершения имеет TabIndex={tabIndex?.ToString() ?? "не задан"}");
                }
            }

            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        private static bool IsTrue(XElement element, string attribute)
            => string.Equals((string?)element.Attribute(attribute), "True", StringComparison.OrdinalIgnoreCase);
    }
}
