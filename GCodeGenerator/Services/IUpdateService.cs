#nullable enable
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GCodeGenerator.Diagnostics;

namespace GCodeGenerator.Services
{
    /// <summary>Найденный выпуск: его версия и страница, откуда его берут.</summary>
    /// <param name="Version">Версия выпуска.</param>
    /// <param name="PageUrl">Страница выпуска на GitHub.</param>
    public sealed record UpdateInfo(ProductVersion Version, string PageUrl);

    /// <summary>
    /// Узнаёт последний выпущенный вариант продукта.
    ///
    /// Обращение к сети — единственное в программе, и происходит оно только
    /// по явному желанию: настройка выключена по умолчанию, а кнопка в окне
    /// «О программе» — это уже действие самого человека. Молча ходить в сеть
    /// программа, которая работает с файлами на своём же компьютере, не должна.
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>
        /// Последний выпуск или <c>null</c>, если узнать его не удалось.
        ///
        /// Отказ — не событие: сети может не быть, GitHub может ответить
        /// отказом, ответ может оказаться не тем. Проверка обновлений — не то,
        /// ради чего стоит показывать пользователю исключение.
        /// </summary>
        /// <param name="cancellation">Отмена ожидания.</param>
        Task<UpdateInfo?> GetLatestReleaseAsync(CancellationToken cancellation = default);
    }

    /// <summary>
    /// Последний выпуск по данным GitHub.
    ///
    /// Запрашивается один документ — описание последнего выпуска
    /// (<c>releases/latest</c>): предвыпуски в него не попадают, и это верно
    /// для программы, которая ставится на станочный компьютер. Ни ключей, ни
    /// учётных данных запрос не требует и ничего о пользователе не сообщает,
    /// кроме того, что неизбежно сообщает любой запрос по сети.
    /// </summary>
    public sealed class GitHubUpdateService : IUpdateService
    {
        /// <summary>Описание последнего выпуска продукта.</summary>
        public const string LatestReleaseUrl =
            "https://api.github.com/repos/AlexanderYunker1983/GCodeGenerator/releases/latest";

        /// <summary>
        /// Дольше этого проверка не ждёт: она нужна к слову, а не любой ценой,
        /// и висящий запрос не должен держать за собой закрытие программы.
        /// </summary>
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly HttpClient _client;
        private readonly IAppLogger _logger;
        private readonly string _requestUrl;

        /// <summary>Служба поверх собственного клиента.</summary>
        /// <param name="logger">Журнал: отказ проверки виден только в нём.</param>
        /// <param name="programInfo">Версия — она уходит в User-Agent запроса.</param>
        public GitHubUpdateService(IAppLogger? logger = null, IProgramInfo? programInfo = null)
            : this(new HttpClient { Timeout = Timeout }, LatestReleaseUrl, logger, programInfo)
        {
        }

        /// <summary>Служба поверх готового клиента — точка подмены для проверок.</summary>
        /// <param name="client">Клиент, которым выполняется запрос.</param>
        /// <param name="requestUrl">Адрес описания последнего выпуска.</param>
        /// <param name="logger">Журнал.</param>
        /// <param name="programInfo">Версия для User-Agent.</param>
        public GitHubUpdateService(
            HttpClient client,
            string requestUrl = LatestReleaseUrl,
            IAppLogger? logger = null,
            IProgramInfo? programInfo = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _requestUrl = requestUrl;
            _logger = logger ?? NullAppLogger.Instance;

            // GitHub отвергает запрос без User-Agent. Называется продукт и его
            // версия — ровно то, что и так видно в запросе, и ничего сверх.
            if (_client.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                var version = programInfo?.Version;
                _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
                    "GCodeGenerator",
                    string.IsNullOrWhiteSpace(version) ? "0.0.0" : Sanitize(version!)));
            }
        }

        /// <inheritdoc />
        public async Task<UpdateInfo?> GetLatestReleaseAsync(CancellationToken cancellation = default)
        {
            try
            {
                using var response = await _client
                    .GetAsync(_requestUrl, HttpCompletionOption.ResponseHeadersRead, cancellation)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Warning($"Update check: GitHub answered {(int)response.StatusCode}");
                    return null;
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, default, cancellation).ConfigureAwait(false);

                return Read(document.RootElement);
            }
            catch (OperationCanceledException)
            {
                // Отмена — не сбой: программа закрывается или проверка
                // не уложилась в отведённое ей время.
                throw;
            }
            catch (Exception failure)
            {
                _logger.Warning($"Update check failed: {failure.Message}");
                return null;
            }
        }

        /// <summary>Версия и страница выпуска из ответа; null — ответ не тот.</summary>
        /// <param name="release">Корень ответа GitHub.</param>
        private UpdateInfo? Read(JsonElement release)
        {
            if (release.ValueKind != JsonValueKind.Object)
                return null;

            var tag = Text(release, "tag_name");
            var version = ProductVersion.Parse(tag);
            if (version == null)
            {
                // Тег вне формата продукта: сравнивать его не с чем, и
                // предлагать обновление на неизвестное — хуже молчания.
                _logger.Warning($"Update check: tag '{tag}' is not a product version");
                return null;
            }

            var page = Text(release, "html_url");
            return new UpdateInfo(
                version,
                string.IsNullOrWhiteSpace(page) ? AboutViewModelReleasesUrl : page!);
        }

        /// <summary>Страница выпусков — запасной адрес, если ответ его не назвал.</summary>
        private const string AboutViewModelReleasesUrl =
            "https://github.com/AlexanderYunker1983/GCodeGenerator/releases";

        private static string? Text(JsonElement element, string name)
            => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        /// <summary>
        /// Значение для User-Agent: заголовок не принимает пробелов и
        /// разделителей, а версия приходит из тега и в норме их не содержит.
        /// </summary>
        /// <param name="version">Версия программы.</param>
        private static string Sanitize(string version)
        {
            var safe = new char[version.Length];
            var length = 0;
            foreach (var symbol in version)
            {
                if (char.IsLetterOrDigit(symbol) || symbol == '.' || symbol == '-')
                    safe[length++] = symbol;
            }

            return length > 0 ? new string(safe, 0, length) : "0.0.0";
        }
    }
}
