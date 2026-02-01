using CommunityToolkit.Mvvm.ComponentModel;

namespace RUNAapp.ViewModels;

/// <summary>
/// Base view model class with common functionality.
/// </summary>
public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;
    
    [ObservableProperty]
    private string _title = string.Empty;
    
    [ObservableProperty]
    private string? _errorMessage;
    
    [ObservableProperty]
    private bool _hasError;
    
    public bool IsNotBusy => !IsBusy;
    
    protected void SetError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }
    
    protected void ClearError()
    {
        ErrorMessage = null;
        HasError = false;
    }
    
    /// <summary>
    /// Executes an async operation with busy state management.
    /// </summary>
    protected async Task ExecuteAsync(Func<Task> operation, string? busyMessage = null)
    {
        if (IsBusy)
            return;
        
        try
        {
            IsBusy = true;
            ClearError();
            await operation();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    /// <summary>
    /// Executes an async operation that returns a value.
    /// </summary>
    protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (IsBusy)
            return default;
        
        try
        {
            IsBusy = true;
            ClearError();
            return await operation();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            return default;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
