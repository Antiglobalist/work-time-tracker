using System.Runtime.InteropServices;

namespace WorkTimeTracking.Services;

public enum HelloResult
{
    Verified,
    Rejected,
    NotAvailable,
    Timeout
}

public class WindowsHelloService
{
    private readonly ILocalizationService _localizationService;

    public WindowsHelloService(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var ucvAvailability = await Windows.Security.Credentials.UI.UserConsentVerifier.CheckAvailabilityAsync();
            return ucvAvailability == Windows.Security.Credentials.UI.UserConsentVerifierAvailability.Available;
        }
        catch
        {
            return false;
        }
    }

    public async Task<HelloResult> VerifyAsync()
    {
        try
        {
            var available = await IsAvailableAsync();
            if (!available)
                return HelloResult.NotAvailable;

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await Windows.Security.Credentials.UI.UserConsentVerifier.RequestVerificationAsync(
                _localizationService["HelloPrompt"]);

            return result switch
            {
                Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified => HelloResult.Verified,
                Windows.Security.Credentials.UI.UserConsentVerificationResult.Canceled => HelloResult.Rejected,
                _ => HelloResult.Rejected
            };
        }
        catch (TaskCanceledException)
        {
            return HelloResult.Timeout;
        }
        catch
        {
            return HelloResult.NotAvailable;
        }
    }
}
