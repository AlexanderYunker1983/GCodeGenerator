using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Свойства файла продукта не расходятся между сборкой, установщиком
    /// и лицензией.
    ///
    /// Издатель, продукт и правообладатель видны пользователю в трёх местах
    /// сразу: в свойствах файла, в окне мастера установки и в списке
    /// установленных программ. Раньше каждое место отвечало за них само, и они
    /// разошлись — издателем сборки значилось её собственное имя, установщик
    /// писал издателя слитно, а год правообладателя в сборке был на четыре
    /// года новее, чем в лицензии. Ничего из этого не ломает ни сборку, ни
    /// прогон: расхождение видно только глазами и только в готовом выпуске.
    /// </summary>
    [TestClass]
    public class ProductMetadataTests
    {
        private static string Root => RepositoryRootLocator.Find();

        private static Assembly Product => typeof(ViewModels.MainViewModel).Assembly;

        /// <summary>Значение свойства из Directory.Build.props.</summary>
        private static string BuildProperty(string name)
        {
            var props = XDocument.Load(Path.Combine(Root, "Directory.Build.props"));
            var value = props.Root?
                .Elements("PropertyGroup")
                .Elements(name)
                .Select(element => element.Value.Trim())
                .FirstOrDefault(text => text.Length > 0);

            Assert.IsNotNull(value, $"Directory.Build.props: не задано свойство <{name}>");
            return value!;
        }

        private static string Attribute<TAttribute>(Func<TAttribute, string> read)
            where TAttribute : Attribute
        {
            var attribute = Product.GetCustomAttribute<TAttribute>();
            Assert.IsNotNull(attribute, $"У сборки нет атрибута {typeof(TAttribute).Name}");
            return read(attribute!);
        }

        /// <summary>
        /// Издатель, продукт и правообладатель приходят в сборку из общего
        /// места, а не подставляются SDK из имени сборки.
        /// </summary>
        [TestMethod]
        public void Assembly_TakesItsMetadataFromTheBuildProperties()
        {
            Assert.AreEqual(BuildProperty("Company"),
                Attribute<AssemblyCompanyAttribute>(a => a.Company), "Издатель");
            Assert.AreEqual(BuildProperty("Product"),
                Attribute<AssemblyProductAttribute>(a => a.Product), "Продукт");
            Assert.AreEqual(BuildProperty("Copyright"),
                Attribute<AssemblyCopyrightAttribute>(a => a.Copyright), "Правообладатель");
        }

        /// <summary>
        /// Издатель — это человек или организация, а не имя сборки. Совпадение
        /// с ним означает, что свойство не задано: именно так и выглядел
        /// прежний дефект — SDK подставлял туда имя сборки, и продукт значился
        /// изданным сам собою. Сверки с общим местом мало: подставить туда
        /// то же имя сборки её не нарушит.
        /// </summary>
        [TestMethod]
        public void Publisher_IsNotTheAssemblyName()
        {
            var assemblyName = Product.GetName().Name;

            Assert.AreNotEqual(assemblyName, BuildProperty("Company"),
                "Издателем значится имя сборки");
            Assert.AreNotEqual(assemblyName, Attribute<AssemblyCompanyAttribute>(a => a.Company),
                "Издателем сборки значится её собственное имя");
        }

        /// <summary>
        /// Заголовок сборки — то, что Windows показывает в свойствах файла как
        /// описание, а диспетчер задач как имя задачи. Совпадение с именем
        /// сборки означает, что его не задали вовсе.
        /// </summary>
        [TestMethod]
        public void Assembly_HasItsOwnDescription()
        {
            var title = Attribute<AssemblyTitleAttribute>(a => a.Title);
            var description = Attribute<AssemblyDescriptionAttribute>(a => a.Description);
            var assemblyName = Product.GetName().Name;

            Assert.AreNotEqual(assemblyName, title,
                "Заголовок сборки совпадает с её именем — значит не задан");
            Assert.IsFalse(string.IsNullOrWhiteSpace(description), "Описание сборки пусто");
        }

        /// <summary>
        /// Правообладатель и годы в сборке совпадают с лицензией. Лицензия
        /// здесь первоисточник: она определяет, кому принадлежит продукт.
        /// </summary>
        [TestMethod]
        public void Copyright_AgreesWithTheLicense()
        {
            var license = File.ReadAllText(Path.Combine(Root, "LICENSE"));
            var inLicense = Regex.Match(license, @"Copyright \(c\) (?<years>[\d\-]+) (?<holder>.+)");
            Assert.IsTrue(inLicense.Success, "LICENSE: не найдена строка правообладателя");

            var copyright = BuildProperty("Copyright");

            StringAssert.Contains(copyright, inLicense.Groups["years"].Value,
                "Годы правообладателя расходятся с лицензией");
            StringAssert.Contains(copyright, inLicense.Groups["holder"].Value.Trim(),
                "Имя правообладателя расходится с лицензией");
        }

        /// <summary>
        /// Установщик берёт те же значения, а не хранит свою копию: именно так
        /// они и разошлись в прошлый раз.
        /// </summary>
        [TestMethod]
        public void Installer_TakesTheSameMetadata()
        {
            var script = File.ReadAllText(Path.Combine(Root, "install", "GCodeGenerator.iss"));

            foreach (var directive in new[]
                     {
                         "AppPublisher", "AppCopyright", "VersionInfoCompany",
                         "VersionInfoCopyright", "VersionInfoProductName",
                     })
            {
                var assignment = Regex.Match(
                    script, @"^" + directive + @"=(?<value>.*)$", RegexOptions.Multiline);

                Assert.IsTrue(assignment.Success, $"install/GCodeGenerator.iss: нет директивы {directive}");
                StringAssert.StartsWith(assignment.Groups["value"].Value.Trim(), "{#",
                    $"{directive} задан своим значением вместо общего — "
                    + "значения разойдутся при первой же правке");
            }

            StringAssert.Contains(
                File.ReadAllText(Path.Combine(Root, "build", "Make-Installer.ps1")),
                "Directory.Build.props",
                "build/Make-Installer.ps1: метаданные не читаются из общего места");
        }

        /// <summary>
        /// README не называет текущую версию продукта. Такая строка устаревает
        /// в день выпуска: до правки она сообщала версию 0.4.0 и обещала, что
        /// релиз ещё впереди, — и именно это увидел бы читатель на странице
        /// готового выпуска.
        /// </summary>
        [TestMethod]
        public void Readme_DoesNotPinTheCurrentVersion()
        {
            var readme = File.ReadAllLines(Path.Combine(Root, "README.md"));
            var offenders = readme
                .Select((text, index) => (Number: index + 1, Text: text))
                .Where(line => Regex.IsMatch(
                    line.Text, @"[Тт]екущая версия|[Cc]urrent version"))
                .Select(line => $"README.md:{line.Number}: {line.Text.Trim()}")
                .ToList();

            Assert.AreEqual(0, offenders.Count,
                "Версия видна в разделе Releases и в заголовке окна — в README она устареет:"
                + Environment.NewLine + string.Join(Environment.NewLine, offenders));
        }
    }
}
