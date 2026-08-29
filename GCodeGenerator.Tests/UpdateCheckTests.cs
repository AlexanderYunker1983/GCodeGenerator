using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Проверка обновлений: разбор версии, запрос к GitHub и согласие на него.
    ///
    /// Обращение к сети у этой программы единственное, поэтому проверяется
    /// не только то, что она узнаёт новую версию, но и то, что без спроса
    /// она в сеть не ходит.
    /// </summary>
    [TestClass]
    public class UpdateCheckTests
    {
        // ------------------------------------------------------------------
        // Сравнение версий
        // ------------------------------------------------------------------

        /// <summary>
        /// Порядок версий тот же, что у сборки: выпуск старше своих
        /// предвыпусков, а внутри класса решает номер.
        /// </summary>
        [TestMethod]
        public void Versions_AreOrderedLikeTheBuildDoesIt()
        {
            var descending = new[]
            {
                "1.2.3", "1.2.3-rc10", "1.2.3-rc5", "1.2.3-beta3", "1.2.3-beta",
                "1.2.3-alpha2", "1.2.3-alpha", "1.2.3-nightly"
            };

            for (var i = 0; i + 1 < descending.Length; i++)
            {
                var newer = ProductVersion.Parse(descending[i])!;
                var older = ProductVersion.Parse(descending[i + 1])!;

                Assert.IsTrue(newer.IsNewerThan(older),
                    $"{descending[i]} должна быть новее {descending[i + 1]}");
                Assert.IsFalse(older.IsNewerThan(newer),
                    $"{descending[i + 1]} не может быть новее {descending[i]}");
            }
        }

        /// <summary>
        /// Числа сравниваются числами. Строкой «1.10.0» меньше «1.9.0» —
        /// именно поэтому версия и разбирается, а не сравнивается как есть.
        /// </summary>
        [TestMethod]
        public void Versions_CompareNumbersAsNumbers()
        {
            Assert.IsTrue(ProductVersion.Parse("1.10.0")!.IsNewerThan(ProductVersion.Parse("1.9.0")));
            Assert.IsTrue(ProductVersion.Parse("2.0.0")!.IsNewerThan(ProductVersion.Parse("1.99.99")));
            Assert.IsTrue(ProductVersion.Parse("0.4.1")!.IsNewerThan(ProductVersion.Parse("0.4.0")));
            Assert.IsFalse(ProductVersion.Parse("0.4.0")!.IsNewerThan(ProductVersion.Parse("0.4.0")));
        }

        /// <summary>
        /// Ведущая «v» отбрасывается — так тег часто показывают на странице
        /// выпусков, — а всё, что на версию не похоже, не разбирается вовсе:
        /// предлагать обновиться на неизвестное хуже, чем промолчать.
        /// </summary>
        [TestMethod]
        public void Versions_RejectWhatIsNotAVersion()
        {
            Assert.AreEqual("1.2.3", ProductVersion.Parse("v1.2.3")!.Text);

            foreach (var text in new[] { null, "", "  ", "1.2", "1.2.3.4", "release", "1.2.3-", "1.2.3 rc5" })
                Assert.IsNull(ProductVersion.Parse(text), $"«{text}» не версия");
        }

        /// <summary>
        /// Старшинство суффиксов совпадает со скриптом версионирования.
        ///
        /// Эти два места решают разные половины одного вопроса: скрипт —
        /// какой версией назваться при сборке, программа — какая версия
        /// новее. Разойдясь, они начнут спорить: программа сочтёт себя
        /// устаревшей относительно того, чем сама и является.
        /// </summary>
        [TestMethod]
        public void SuffixOrder_AgreesWithTheVersioningScript()
        {
            var script = File.ReadAllText(Path.Combine(
                RepositoryRootLocator.Find(), "build", "Get-GitVersion.ps1"));

            var ranks = Regex.Matches(script, @"'(?<class>alpha|beta|rc)'\s*\{\s*\$classRank\s*=\s*(?<rank>\d+)\s*\}")
                .Cast<Match>()
                .ToDictionary(match => match.Groups["class"].Value, match => int.Parse(match.Groups["rank"].Value));

            Assert.AreEqual(3, ranks.Count, "Скрипт перечисляет не три класса — проверку нужно обновить");

            foreach (var pair in ranks)
            {
                Assert.AreEqual(pair.Value, ProductVersion.Parse($"1.0.0-{pair.Key}")!.ClassRank,
                    $"Старшинство «{pair.Key}» разошлось со скриптом версионирования");
            }

            Assert.AreEqual(4, ProductVersion.Parse("1.0.0")!.ClassRank, "Выпуск старше любого предвыпуска");
            Assert.AreEqual(0, ProductVersion.Parse("1.0.0-nightly")!.ClassRank, "Незнакомый суффикс — самый младший");
        }

        // ------------------------------------------------------------------
        // Запрос к GitHub
        // ------------------------------------------------------------------

        /// <summary>Ответ разбирается: версия из тега, адрес — страница выпуска.</summary>
        [TestMethod]
        public async Task Service_ReadsTheTagAndThePage()
        {
            using var service = Service(HttpStatusCode.OK,
                @"{ ""tag_name"": ""1.2.3"", ""html_url"": ""https://github.com/AlexanderYunker1983/GCodeGenerator/releases/tag/1.2.3"" }");

            var answer = await service.Service.GetLatestReleaseAsync();

            Assert.IsNotNull(answer.Release);
            Assert.AreEqual("1.2.3", answer.Release!.Version.Text);
            Assert.AreEqual(
                "https://github.com/AlexanderYunker1983/GCodeGenerator/releases/tag/1.2.3",
                answer.Release.PageUrl);
            Assert.AreEqual(string.Empty, answer.Detail, "У успеха причины нет");
        }

        [TestMethod]
        [DataRow("file:///C:/Windows/System32/calc.exe")]
        [DataRow("https://github.com.evil.example/AlexanderYunker1983/GCodeGenerator/releases/tag/9.9.9")]
        [DataRow("http://github.com/AlexanderYunker1983/GCodeGenerator/releases/tag/9.9.9")]
        [DataRow("https://github.com/AlexanderYunker1983/GCodeGenerator/issues/1")]
        public async Task Service_DoesNotExposeUntrustedReleaseLinks(string page)
        {
            using var service = Service(HttpStatusCode.OK,
                $@"{{ ""tag_name"": ""9.9.9"", ""html_url"": ""{page}"" }}");

            var answer = await service.Service.GetLatestReleaseAsync();

            Assert.AreEqual(
                "https://github.com/AlexanderYunker1983/GCodeGenerator/releases",
                answer.Release!.PageUrl);
        }

        /// <summary>
        /// GitHub отвергает запрос без User-Agent, и проверка молча
        /// перестала бы работать. Заголовок называет продукт и версию —
        /// ровно то, что и так видно в запросе.
        /// </summary>
        [TestMethod]
        public async Task Service_IntroducesItself()
        {
            using var service = Service(HttpStatusCode.OK, @"{ ""tag_name"": ""1.2.3"" }");

            await service.Service.GetLatestReleaseAsync();

            var agent = service.Handler.LastRequest!.Headers.UserAgent.ToString();
            StringAssert.Contains(agent, "GCodeGenerator", "Запрос не представляется");
        }

        /// <summary>
        /// Всё, что мешает узнать версию, — не событие: сети может не быть,
        /// GitHub может ответить отказом, ответ может оказаться не тем.
        /// Проверка обновлений не то, ради чего показывают исключение.
        /// </summary>
        [TestMethod]
        [DataRow(HttpStatusCode.NotFound, @"{ ""tag_name"": ""9.9.9"" }", DisplayName = "отказ GitHub")]
        [DataRow(HttpStatusCode.OK, "не JSON вовсе", DisplayName = "ответ не разбирается")]
        [DataRow(HttpStatusCode.OK, @"{ ""tag_name"": ""nightly-2026-08-28"" }", DisplayName = "тег вне формата")]
        [DataRow(HttpStatusCode.OK, "[]", DisplayName = "ответ не об одном выпуске")]
        public async Task Service_StaysSilentOnAnythingUnexpected(HttpStatusCode status, string body)
        {
            using var service = Service(status, body);

            var answer = await service.Service.GetLatestReleaseAsync();

            Assert.IsNull(answer.Release, "Выпуск не должен был найтись");
            Assert.AreNotEqual(string.Empty, answer.Detail,
                "Отказ без причины отправляет читать журнал");
        }

        /// <summary>Отказ сети не выходит наружу исключением.</summary>
        [TestMethod]
        public async Task Service_SurvivesNoNetwork()
        {
            var handler = new FakeHandler(_ => throw new HttpRequestException(
                "запрос не удался", new System.Net.Sockets.SocketException(10013)));
            using var client = new HttpClient(handler);
            var service = new GitHubUpdateService(client);

            var answer = await service.GetLatestReleaseAsync();

            Assert.IsNull(answer.Release);

            // В окно уходит причина самой глубокой обёртки: внешняя говорит
            // лишь, что запрос не удался, а по делу отвечает сокет.
            Assert.AreNotEqual("запрос не удался", answer.Detail,
                "Показана обёртка вместо причины");
            Assert.AreNotEqual(string.Empty, answer.Detail);
        }

        /// <summary>
        /// Исчерпанный предел числа обращений назван отдельно: это не сбой,
        /// он проходит сам через час, и совет при нём другой. Шестьдесят
        /// обращений в час GitHub считает на адрес, а не на программу, —
        /// исчерпать их может и не она.
        /// </summary>
        [TestMethod]
        public async Task Service_TellsRateLimitingApartFromAFailure()
        {
            var handler = new FakeHandler(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
                response.Headers.Add("x-ratelimit-remaining", "0");
                return response;
            });
            using var client = new HttpClient(handler);

            var answer = await new GitHubUpdateService(client).GetLatestReleaseAsync();

            Assert.IsTrue(answer.IsRateLimited, "Ограничение числа запросов не распознано");
            Assert.IsNull(answer.Release);
        }

        /// <summary>
        /// Тот же отказ без признака исчерпания — обычный сбой: 403 приходит
        /// и по другим причинам, и объяснять его ожиданием часа неверно.
        /// </summary>
        [TestMethod]
        public async Task Service_DoesNotCallEveryRefusalRateLimiting()
        {
            using var service = Service(HttpStatusCode.Forbidden, "{}");

            var answer = await service.Service.GetLatestReleaseAsync();

            Assert.IsFalse(answer.IsRateLimited);
            StringAssert.Contains(answer.Detail, "403", "Код ответа не назван");
        }

        // ------------------------------------------------------------------
        // Согласие
        // ------------------------------------------------------------------

        /// <summary>
        /// Без согласия программа в сеть не ходит. Это и есть главное
        /// свойство всей затеи: настройка выключена по умолчанию, и пока
        /// её не включили, запроса не происходит вовсе.
        /// </summary>
        [TestMethod]
        public async Task Startup_DoesNotGoOnlineWithoutConsent()
        {
            var updates = new CountingUpdateService("9.9.9");
            var (main, _, _, store) = MainViewModelOperationEditTests.CreateMain(updates: updates);

            Assert.IsFalse(store.Current.Ui.CheckForUpdates, "Проверка выключена по умолчанию");
            await updates.Settle();

            Assert.AreEqual(0, updates.Calls, "Программа обратилась в сеть, никого не спросив");
            Assert.IsFalse(main.HasUpdate);
        }

        /// <summary>С согласия — спрашивает и сообщает о вышедшей версии.</summary>
        [TestMethod]
        public async Task Startup_ReportsANewerVersionWhenAsked()
        {
            var updates = new CountingUpdateService("9.9.9");
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain(
                localizationManager: Russian(), updates: updates, checkForUpdates: true);

            await updates.Settle();
            await WaitUntil(() => main.HasUpdate);

            Assert.AreEqual("Доступна версия 9.9.9", main.UpdateNotice);
            Assert.AreEqual(1, updates.Calls);
            Assert.IsTrue(main.OpenUpdatePageCommand.CanExecute(null), "Есть куда перейти");
        }

        /// <summary>
        /// Установленная версия — последняя: сообщать нечего, и строка
        /// в окне не появляется.
        /// </summary>
        [TestMethod]
        public async Task Startup_SaysNothingWhenUpToDate()
        {
            var updates = new CountingUpdateService("1.0.0");
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain(
                updates: updates, checkForUpdates: true);

            await updates.Settle();

            Assert.AreEqual(1, updates.Calls, "Проверка была");
            Assert.IsFalse(main.HasUpdate, "Сообщать не о чем");
        }

        // ------------------------------------------------------------------
        // Окно «О программе»
        // ------------------------------------------------------------------

        /// <summary>
        /// Кнопка проверяет независимо от настройки: нажатие и есть
        /// согласие. Найденный выпуск можно открыть.
        /// </summary>
        [TestMethod]
        public async Task About_ChecksOnDemandAndOffersThePage()
        {
            var about = new AboutViewModelBuilder("1.0.0", new CountingUpdateService("2.0.0")).Build();

            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)about.CheckUpdatesCommand).ExecuteAsync(null);

            Assert.AreEqual("Доступна версия 2.0.0", about.UpdateStatus);
            Assert.IsTrue(about.HasNewerVersion);
            Assert.IsTrue(about.OpenUpdatePageCommand.CanExecute(null));
        }

        /// <summary>Последняя версия — так и сказано, переходить некуда.</summary>
        [TestMethod]
        public async Task About_SaysWhenNothingIsNewer()
        {
            var about = new AboutViewModelBuilder("2.0.0", new CountingUpdateService("2.0.0")).Build();

            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)about.CheckUpdatesCommand).ExecuteAsync(null);

            Assert.IsTrue(about.HasUpdateStatus);
            Assert.IsFalse(about.HasNewerVersion, "Переходить некуда");
            Assert.IsFalse(about.OpenUpdatePageCommand.CanExecute(null));
        }

        /// <summary>
        /// Проверка не удалась — окно называет причину, а не отправляет
        /// в журнал. Прежде оно писало «причина — в журнале работы», и ради
        /// одной строки, которая у программы уже была, приходилось открывать
        /// файл в недрах профиля.
        /// </summary>
        [TestMethod]
        public async Task About_ReportsWhyTheCheckFailed()
        {
            var about = new AboutViewModelBuilder("1.0.0", new CountingUpdateService(null)).Build();

            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)about.CheckUpdatesCommand).ExecuteAsync(null);

            Assert.IsTrue(about.HasUpdateStatus, "Молчание неотличимо от «всё в порядке»");
            Assert.IsFalse(about.HasNewerVersion);
            StringAssert.Contains(about.UpdateStatus, "узнать не удалось",
                "Причина отказа до окна не дошла");
        }

        /// <summary>
        /// Исчерпанный предел обращений объясняется человеку, а не кодом
        /// ответа: ждать час — это совет, «HTTP 403» — нет.
        /// </summary>
        [TestMethod]
        public async Task About_ExplainsRateLimiting()
        {
            var about = new AboutViewModelBuilder("1.0.0", new RateLimitedUpdateService()).Build();

            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)about.CheckUpdatesCommand).ExecuteAsync(null);

            StringAssert.Contains(about.UpdateStatus, "GitHub");
            StringAssert.Contains(about.UpdateStatus, "час", "Не сказано, когда пробовать снова");
            Assert.IsFalse(about.UpdateStatus.Contains("403"), "Код ответа человеку ничего не говорит");
        }

        /// <summary>Служба, всегда отвечающая исчерпанным пределом обращений.</summary>
        private sealed class RateLimitedUpdateService : IUpdateService
        {
            public Task<UpdateCheckResult> GetLatestReleaseAsync(CancellationToken cancellation = default)
                => Task.FromResult(UpdateCheckResult.RateLimited());
        }

        // ------------------------------------------------------------------
        // Вспомогательное
        // ------------------------------------------------------------------

        private sealed class AboutViewModelBuilder
        {
            private readonly string _version;
            private readonly IUpdateService _updates;

            public AboutViewModelBuilder(string version, IUpdateService updates)
            {
                _version = version;
                _updates = updates;
            }

            public ViewModels.AboutViewModel Build()
                => new ViewModels.AboutViewModel(
                    Russian(), new ProgramInfo(_version), null, _updates);
        }

        /// <summary>
        /// Настоящий словарь на русском: проверяется тот же путь, каким текст
        /// собирается в работающей программе, — вместе с подстановкой версии
        /// в переведённый шаблон.
        /// </summary>
        private static GCodeGenerator.Localization.LocalizationManager Russian()
        {
            var manager = new GCodeGenerator.Localization.LocalizationManager();
            manager.AddAssembly("GCodeGenerator");
            manager.ChangeCulture(new System.Globalization.CultureInfo("ru"));
            return manager;
        }

        /// <summary>Служба обновлений, считающая обращения.</summary>
        private sealed class CountingUpdateService : IUpdateService
        {
            private readonly string _latest;
            private readonly TaskCompletionSource _asked = new TaskCompletionSource();

            /// <param name="latest">Версия последнего выпуска; null — узнать не удалось.</param>
            public CountingUpdateService(string latest)
            {
                _latest = latest;
            }

            public int Calls { get; private set; }

            public Task<UpdateCheckResult> GetLatestReleaseAsync(CancellationToken cancellation = default)
            {
                Calls++;
                _asked.TrySetResult();

                var version = ProductVersion.Parse(_latest);
                return Task.FromResult(version == null
                    ? UpdateCheckResult.Failed("узнать не удалось")
                    : UpdateCheckResult.Found(new UpdateInfo(version, "https://example.invalid/release")));
            }

            /// <summary>
            /// Ждёт, пока проверка успеет случиться, — и заведомо дольше,
            /// чем ей нужно, если она вообще не начнётся: тест «в сеть не
            /// ходили» иначе проходил бы просто потому, что не дождался.
            /// </summary>
            public async Task Settle()
                => await Task.WhenAny(_asked.Task, Task.Delay(500));
        }

        /// <summary>Ответ на запрос без сети.</summary>
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _answer;

            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> answer)
            {
                _answer = answer;
            }

            public HttpRequestMessage LastRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(_answer(request));
            }
        }

        private sealed class ServiceUnderTest : IDisposable
        {
            public ServiceUnderTest(FakeHandler handler, HttpClient client, GitHubUpdateService service)
            {
                Handler = handler;
                Client = client;
                Service = service;
            }

            public FakeHandler Handler { get; }

            private HttpClient Client { get; }

            public GitHubUpdateService Service { get; }

            public void Dispose() => Client.Dispose();
        }

        private static ServiceUnderTest Service(HttpStatusCode status, string body)
        {
            var handler = new FakeHandler(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler);
            return new ServiceUnderTest(handler, client, new GitHubUpdateService(client));
        }

        /// <summary>Ждёт условия; тест не должен зависнуть насовсем.</summary>
        private static async Task WaitUntil(Func<bool> condition)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                if (condition())
                    return;
                await Task.Delay(10);
            }

            Assert.Fail("Условие так и не выполнилось");
        }
    }
}
