using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Provides deterministic intent classification for critical commands
/// when remote AI intent parsing is unavailable.
/// </summary>
public interface IDeterministicIntentService
{
    bool TryClassify(string transcript, out IntentResult result);
}
