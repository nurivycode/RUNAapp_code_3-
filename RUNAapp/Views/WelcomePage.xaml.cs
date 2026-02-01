using RUNAapp.ViewModels;

namespace RUNAapp.Views;

public partial class WelcomePage : ContentPage
{
    public WelcomePage(WelcomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is WelcomeViewModel vm)
        {
            await vm.InitializeCommand.ExecuteAsync(null);
        }
    }
}
