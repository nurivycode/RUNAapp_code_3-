using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using RUNAapp.Helpers;
using RUNAapp.Models;

namespace RUNAapp.Services;

public class OpenAIService : IOpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public OpenAIService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
    
    public async Task<string> TranscribeAudioAsync(byte[] audioData, string fileName, string? language = null)
    {
        EnsureBackendConfigured();
        return await TranscribeViaBackendAsync(audioData, fileName, language);
    }
    
    private async Task<string> TranscribeViaBackendAsync(byte[] audioData, string fileName, string? language = null)
    {
        var url = $"{Constants.BackendBaseUrl}/transcribe";
        
        var audioBase64 = Convert.ToBase64String(audioData);
        var requestBody = new
        {
            audioBase64 = audioBase64,
            fileName = fileName,
            language = language
        };
        
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = JsonContent.Create(requestBody, options: _jsonOptions);
        
        var response = await _httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new OpenAIException($"Backend transcription failed: {response.StatusCode} - {errorContent}");
        }
        
        var result = await response.Content.ReadFromJsonAsync<BackendTranscribeResponse>(_jsonOptions);
        return result?.Text ?? string.Empty;
    }
    
    public async Task<IntentResult> ClassifyIntentAsync(string transcript)
    {
        EnsureBackendConfigured();
        return await ClassifyIntentViaBackendAsync(transcript);
    }
    
    public async Task<string> GetChatResponseAsync(string userMessage, string? systemPrompt = null)
    {
        EnsureBackendConfigured();
        return await GetChatResponseViaBackendAsync(userMessage, systemPrompt);
    }
    
    private async Task<string> GetChatResponseViaBackendAsync(string userMessage, string? systemPrompt = null)
    {
        var url = $"{Constants.BackendBaseUrl}/chat";
        
        var messages = new List<OpenAIChatMessage>();
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new OpenAIChatMessage { Role = "system", Content = systemPrompt });
        }
        messages.Add(new OpenAIChatMessage { Role = "user", Content = userMessage });
        
        var requestBody = new
        {
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            model = Constants.OpenAIChatModel,
            temperature = 0.7f,
            maxTokens = 512
        };
        
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = JsonContent.Create(requestBody, options: _jsonOptions);
        
        var response = await _httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new OpenAIException($"Backend chat failed: {response.StatusCode} - {errorContent}");
        }
        
        var chatResponse = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>(_jsonOptions);
        return chatResponse?.Choices?.FirstOrDefault()?.Message.Content ?? string.Empty;
    }
    
    private async Task<IntentResult> ClassifyIntentViaBackendAsync(string transcript)
    {
        var url = $"{Constants.BackendBaseUrl}/classifyIntent";
        
        var requestBody = new
        {
            transcript = transcript
        };
        
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = JsonContent.Create(requestBody, options: _jsonOptions);
        
        var response = await _httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new OpenAIException($"Backend intent classification failed: {response.StatusCode} - {errorContent}");
        }
        
        var result = await response.Content.ReadFromJsonAsync<BackendClassifyIntentResponse>(_jsonOptions);
        
        if (result == null)
            throw new OpenAIException("Failed to parse backend intent classification response");
        
        return new IntentResult
        {
            Action = ParseIntentAction(result.Action),
            Confidence = result.Confidence,
            Parameters = result.Parameters ?? new Dictionary<string, string>(),
            Response = result.Response,
            RequiresFollowUp = result.RequiresFollowUp,
            FollowUpQuestion = result.FollowUpQuestion,
            OriginalTranscript = transcript
        };
    }
    
    private static IntentAction ParseIntentAction(string action)
    {
        return action.ToLower() switch
        {
            "navigateto" => IntentAction.NavigateTo,
            "getdirections" => IntentAction.GetDirections,
            "startnavigation" => IntentAction.StartNavigation,
            "stopnavigation" => IntentAction.StopNavigation,
            "whereami" => IntentAction.WhereAmI,
            "startdetection" => IntentAction.StartDetection,
            "stopdetection" => IntentAction.StopDetection,
            "describesurroundings" => IntentAction.DescribeSurroundings,
            "checkstatus" => IntentAction.CheckStatus,
            "gethelp" => IntentAction.GetHelp,
            "repeatlastmessage" => IntentAction.RepeatLastMessage,
            "confirm" => IntentAction.Confirm,
            "cancel" => IntentAction.Cancel,
            _ => IntentAction.Unknown
        };
    }

    private static void EnsureBackendConfigured()
    {
        if (string.IsNullOrWhiteSpace(Constants.BackendBaseUrl))
            throw new InvalidOperationException("BackendBaseUrl not configured.");
    }
}

public class OpenAIException : Exception
{
    public OpenAIException(string message) : base(message) { }
    public OpenAIException(string message, Exception inner) : base(message, inner) { }
}
