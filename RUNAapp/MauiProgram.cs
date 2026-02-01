using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using RUNAapp.Services;
using RUNAapp.ViewModels;
using RUNAapp.Views;
#if ANDROID
using RUNAapp.Platforms.Android;
#endif

namespace RUNAapp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ═══════════════════════════════════════════════════════════════════════
        // HTTP Client
        // ═══════════════════════════════════════════════════════════════════════
        builder.Services.AddSingleton<HttpClient>();

        // ═══════════════════════════════════════════════════════════════════════
        // Audio Manager (for voice recording)
        // ═══════════════════════════════════════════════════════════════════════
        builder.Services.AddSingleton(AudioManager.Current);

        // ═══════════════════════════════════════════════════════════════════════
        // Services - Singleton (shared across app)
        // ═══════════════════════════════════════════════════════════════════════
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IOpenAIService, OpenAIService>();
        builder.Services.AddSingleton<ITextToSpeechService, TextToSpeechService>();
        builder.Services.AddSingleton<IComputerVisionService, ComputerVisionService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IVoiceAssistantService, VoiceAssistantService>();
        builder.Services.AddSingleton<IFirestoreService, FirestoreService>();
        
#if ANDROID
        builder.Services.AddSingleton<ICameraFrameProvider, CameraFrameHandler>();
#else
        // iOS implementation placeholder
        // builder.Services.AddSingleton<ICameraFrameProvider, IosCameraFrameHandler>();
#endif

        // ═══════════════════════════════════════════════════════════════════════
        // ViewModels - Transient (new instance per request)
        // ═══════════════════════════════════════════════════════════════════════
        builder.Services.AddTransient<WelcomeViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<NavigationViewModel>();
        builder.Services.AddTransient<VisionViewModel>();
        builder.Services.AddTransient<SetupViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // ═══════════════════════════════════════════════════════════════════════
        // Pages - Transient (new instance per navigation)
        // ═══════════════════════════════════════════════════════════════════════
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<Views.NavigationPage>();
        builder.Services.AddTransient<VisionPage>();
        builder.Services.AddTransient<SetupPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
