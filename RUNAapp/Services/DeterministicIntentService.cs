using System.Text.RegularExpressions;
using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Deterministic command classifier for critical offline-safe commands.
/// Keeps core navigation usable even when remote intent APIs fail.
/// </summary>
public partial class DeterministicIntentService : IDeterministicIntentService
{
    private static readonly Regex StartNavigationRegex = StartNavigationRegexFactory();
    private static readonly Regex StopNavigationRegex = StopNavigationRegexFactory();
    private static readonly Regex WhereAmIRegex = WhereAmIRegexFactory();
    private static readonly Regex HelpRegex = HelpRegexFactory();
    private static readonly Regex RepeatRegex = RepeatRegexFactory();
    private static readonly Regex StartDetectionRegex = StartDetectionRegexFactory();
    private static readonly Regex StopDetectionRegex = StopDetectionRegexFactory();
    private static readonly Regex DescribeSurroundingsRegex = DescribeSurroundingsRegexFactory();
    private static readonly Regex[] DestinationRegexes =
    [
        EnglishNavigateRegexFactory(),
        RussianNavigateRegexFactory(),
        CommandPrefixNavigateRegexFactory()
    ];

    public bool TryClassify(string transcript, out IntentResult result)
    {
        result = new IntentResult();

        if (string.IsNullOrWhiteSpace(transcript))
            return false;

        var text = transcript.Trim();

        if (StartNavigationRegex.IsMatch(text))
        {
            result = CreateResult(
                IntentAction.StartNavigation,
                text,
                response: "Starting navigation.");
            return true;
        }

        if (StopNavigationRegex.IsMatch(text))
        {
            result = CreateResult(
                IntentAction.StopNavigation,
                text,
                response: "Stopping navigation.");
            return true;
        }

        if (WhereAmIRegex.IsMatch(text))
        {
            result = CreateResult(
                IntentAction.WhereAmI,
                text,
                response: "Checking your current location.");
            return true;
        }

        if (HelpRegex.IsMatch(text))
        {
            result = CreateResult(
                IntentAction.GetHelp,
                text,
                response: "Here are the available commands.");
            return true;
        }

        if (RepeatRegex.IsMatch(text))
        {
            result = CreateResult(
                IntentAction.RepeatLastMessage,
                text,
                response: "Repeating the last message.");
            return true;
        }

        if (StartDetectionRegex.IsMatch(text))
        {
            result = CreateResult(
                IntentAction.StartDetection,
                text,
                response: "Starting obstacle detection.");
            return true;
        }

        if (StopDetectionRegex.IsMatch(text))
        {
            result = CreateResult(
                IntentAction.StopDetection,
                text,
                response: "Stopping detection.");
            return true;
        }

        if (DescribeSurroundingsRegex.IsMatch(text))
        {
            result = CreateResult(
                IntentAction.DescribeSurroundings,
                text,
                response: "Describing your surroundings.");
            return true;
        }

        if (TryExtractDestination(text, out var destination))
        {
            result = CreateResult(
                IntentAction.NavigateTo,
                text,
                response: $"Opening navigation to {destination}.",
                parameters: new Dictionary<string, string>
                {
                    ["destination"] = destination
                });
            return true;
        }

        return false;
    }

    private static bool TryExtractDestination(string text, out string destination)
    {
        destination = string.Empty;

        foreach (var regex in DestinationRegexes)
        {
            var match = regex.Match(text);
            if (!match.Success)
                continue;

            destination = NormalizeDestination(match.Groups["dest"].Value);
            if (!string.IsNullOrWhiteSpace(destination))
                return true;
        }

        return false;
    }

    private static string NormalizeDestination(string destination)
    {
        var normalized = destination.Trim().Trim('"', '\'', '.', ',', ';', ':', '!', '?');
        return normalized.Length < 2 ? string.Empty : normalized;
    }

    private static IntentResult CreateResult(
        IntentAction action,
        string originalTranscript,
        string response,
        Dictionary<string, string>? parameters = null)
    {
        return new IntentResult
        {
            Action = action,
            Confidence = 0.95f,
            Parameters = parameters ?? new Dictionary<string, string>(),
            Response = response,
            RequiresFollowUp = false,
            FollowUpQuestion = null,
            OriginalTranscript = originalTranscript
        };
    }

    [GeneratedRegex(@"^\s*(start|start navigation|begin navigation|nachni navigaciyu|nachat navigaciyu|\u043D\u0430\u0447\u043D\u0438 \u043D\u0430\u0432\u0438\u0433\u0430\u0446\u0438\u044E|\u043D\u0430\u0447\u0430\u0442\u044C \u043D\u0430\u0432\u0438\u0433\u0430\u0446\u0438\u044E|\u0441\u0442\u0430\u0440\u0442 \u043D\u0430\u0432\u0438\u0433\u0430\u0446\u0438\u0438|\u043F\u043E\u0435\u0445\u0430\u043B\u0438)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StartNavigationRegexFactory();

    [GeneratedRegex(@"^\s*(stop|stop navigation|stop nav|ostanovi navigaciyu|ostanovit navigaciyu|\u043E\u0441\u0442\u0430\u043D\u043E\u0432\u0438 \u043D\u0430\u0432\u0438\u0433\u0430\u0446\u0438\u044E|\u043E\u0441\u0442\u0430\u043D\u043E\u0432\u0438\u0442\u044C \u043D\u0430\u0432\u0438\u0433\u0430\u0446\u0438\u044E|\u0441\u0442\u043E\u043F \u043D\u0430\u0432\u0438\u0433\u0430\u0446\u0438\u0438|\u0445\u0432\u0430\u0442\u0438\u0442)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StopNavigationRegexFactory();

    [GeneratedRegex(@"^\s*(where am i|my location|gde ya|gde ya seichas|\u0433\u0434\u0435 \u044F|\u0433\u0434\u0435 \u044F \u0441\u0435\u0439\u0447\u0430\u0441)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WhereAmIRegexFactory();

    [GeneratedRegex(@"^\s*(help|what can you do|pomosh|chto ty umeesh|\u043F\u043E\u043C\u043E\u0449\u044C|\u0447\u0442\u043E \u0442\u044B \u0443\u043C\u0435\u0435\u0448\u044C)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HelpRegexFactory();

    [GeneratedRegex(@"^\s*(repeat|repeat that|povtori|povtori soobshenie|\u043F\u043E\u0432\u0442\u043E\u0440\u0438|\u043F\u043E\u0432\u0442\u043E\u0440\u0438 \u0441\u043E\u043E\u0431\u0449\u0435\u043D\u0438\u0435)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RepeatRegexFactory();

    [GeneratedRegex(@"^\s*(?:navigate|route|go|take me|guide me)\s+(?:to\s+)?(?<dest>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishNavigateRegexFactory();

    [GeneratedRegex(@"^\s*(?:vedi|marshrut|prolozhi marshrut|put|\u0432\u0435\u0434\u0438|\u043C\u0430\u0440\u0448\u0440\u0443\u0442|\u043F\u0440\u043E\u043B\u043E\u0436\u0438 \u043C\u0430\u0440\u0448\u0440\u0443\u0442|\u043F\u0443\u0442\u044C)\s+(?:k|do|\u043A|\u0434\u043E)?\s*(?<dest>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RussianNavigateRegexFactory();

    [GeneratedRegex(@"^\s*(?:/nav|nav|destination)\s+(?<dest>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CommandPrefixNavigateRegexFactory();

    [GeneratedRegex(@"^\s*(start detection|start looking|start obstacle detection|what is ahead|what's ahead|look around|начни обнаружение|начать обнаружение|что впереди|осмотрись)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StartDetectionRegexFactory();

    [GeneratedRegex(@"^\s*(stop detection|stop looking|stop obstacle detection|остановить обнаружение|стоп обнаружение|хватит смотреть)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StopDetectionRegexFactory();

    [GeneratedRegex(@"^\s*(describe surroundings|describe what you see|what do you see|what's around me|what is around me|опиши окружение|что вокруг|что ты видишь|что вокруг меня)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DescribeSurroundingsRegexFactory();
}
