#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GCodeGenerator.Diagnostics;

namespace GCodeGenerator.Services
{
    /// <summary>Найденный выпуск: его версия и проверенная страница на GitHub.</summary>
    public sealed record UpdateInfo
    {
        public UpdateInfo(ProductVersion version, string? pageUrl)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            PageUrl = ReleasePageAddress.Normalize(pageUrl);
        }

        public ProductVersion Version { get; }

        public string PageUrl { get; }
    }

    /// <summary>Единственная область ссылок, которую можно открыть из сетевого ответа.</summary>
    internal static class ReleasePageAddress
    {
        public const string Fallback =
            "https://github.com/AlexanderYunker1983/GCodeGenerator/releases";

        private const string ReleasePath =
            "/AlexanderYunker1983/GCodeGenerator/releases";

        public static string Normalize(string? value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                || !uri.IsDefaultPort
                || uri.UserInfo.Length != 0)
            {
                return Fallback;
            }

            var path = uri.AbsolutePath.TrimEnd('/');
            return path.Equals(ReleasePath, StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(ReleasePath + "/", StringComparison.OrdinalIgnoreCase)
                ? uri.AbsoluteUri
                : Fallback;
        }
    }

    /// <summary>
    /// Чем закончился вопрос о последнем выпуске.
    ///
    /// Отказ несёт причину, а не одну свою пустоту: «не удалось, смотрите
    /// в журнале» отправляет человека искать файл ради одной строки, которая
    /// и так уже написана. Причина приходит текстом от того, кто отказал, —
    /// системой или самим GitHub.
    /// </summary>
    /// <param name="Release">Найденный выпуск или <c>null</c>.</param>
    /// <param name="Detail">Причина отказа; пусто — отказа не было.</param>
    /// <param name="IsRateLimited">
    /// GitHub ограничил число запросов. Единственная причина, названная
    /// отдельно: она проходит сама и объясняется человеку иначе, чем сбой.
    /// </param>
    public sealed record UpdateCheckResult(UpdateInfo? Release, string Detail, bool IsRateLimited)
    {
        /// <summary>Выпуск найден.</summary>
        public static UpdateCheckResult Found(UpdateInfo release)
            => new UpdateCheckResult(release, string.Empty, false);

        /// <summary>Узнать не удалось; причина — текстом.</summary>
        public static UpdateCheckResult Failed(string detail)
            => new UpdateCheckResult(null, detail, false);

        /// <summary>GitHub ограничил число запросов.</summary>
        public static UpdateCheckResult RateLimited()
            => new UpdateCheckResult(null, string.Empty, true);
    }

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
        /// Последний выпуск либо причина, по которой узнать его не удалось.
        ///
        /// Отказ — не событие: сети может не быть, GitHub может ответить
        /// отказом, ответ может оказаться не тем. Проверка обновлений — не то,
        /// ради чего стоит показывать пользователю исключение, но и молчать
        /// о причине незачем: она у отказавшего есть.
        /// </summary>
        /// <param name="cancellation">Отмена ожидания.</param>
        Task<UpdateCheckResult> GetLatestReleaseAsync(CancellationToken cancellation = default);
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
        public async Task<UpdateCheckResult> GetLatestReleaseAsync(CancellationToken cancellation = default)
        {
            try
            {
                using var response = await _client
                    .GetAsync(_requestUrl, HttpCompletionOption.ResponseHeadersRead, cancellation)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var status = (int)response.StatusCode;
                    _logger.Warning($"Update check: GitHub answered {status}");

                    // Обращений без учётной записи GitHub разрешает шестьдесят
                    // в час на адрес, а исчерпать их может и не эта программа:
                    // счёт общий на всех, кто выходит через тот же адрес.
                    // Это не сбой и проходит само, поэтому названо отдельно.
                    return IsRateLimited(response)
                        ? UpdateCheckResult.RateLimited()
                        : UpdateCheckResult.Failed($"HTTP {status}");
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

                // Текст исключения говорит по делу — «узел не найден»,
                // «доступ к сокету запрещён», — и говорит на языке системы.
                // Он и уходит в окно: гадать о причине не приходится ни
                // пользователю, ни тому, кто разбирает его сообщение.
                return UpdateCheckResult.Failed(Innermost(failure).Message);
            }
        }

        /// <summary>Ответ означает исчерпанный предел числа обращений.</summary>
        /// <param name="response">Ответ GitHub.</param>
        private static bool IsRateLimited(HttpResponseMessage response)
        {
            if (response.StatusCode != HttpStatusCode.Forbidden
                && response.StatusCode != HttpStatusCode.TooManyRequests)
            {
                return false;
            }

            return response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining)
                && remaining.FirstOrDefault() == "0";
        }

        /// <summary>
        /// Самое внутреннее исключение: обёртка сообщает лишь, что запрос
        /// не удался, а причину называет то, что лежит под ней.
        /// </summary>
        /// <param name="failure">Пойманное исключение.</param>
        private static Exception Innermost(Exception failure)
        {
            var current = failure;
            while (current.InnerException != null)
                current = current.InnerException;
            return current;
        }

        /// <summary>Версия и страница выпуска из ответа либо причина отказа.</summary>
        /// <param name="release">Корень ответа GitHub.</param>
        private UpdateCheckResult Read(JsonElement release)
        {
            if (release.ValueKind != JsonValueKind.Object)
                return UpdateCheckResult.Failed(release.ValueKind.ToString());

            var tag = Text(release, "tag_name");
            var version = ProductVersion.Parse(tag);
            if (version == null)
            {
                // Тег вне формата продукта: сравнивать его не с чем, и
                // предлагать обновление на неизвестное — хуже молчания.
                _logger.Warning($"Update check: tag '{tag}' is not a product version");
                return UpdateCheckResult.Failed($"tag_name: {tag}");
            }

            var page = Text(release, "html_url");
            return UpdateCheckResult.Found(new UpdateInfo(
                version,
                page));
        }

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
