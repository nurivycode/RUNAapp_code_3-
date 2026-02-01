using RUNAapp.ViewModels;

namespace RUNAapp.Views;

public partial class NavigationPage : ContentPage
{
    public NavigationPage(NavigationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is NavigationViewModel vm)
        {
            await vm.InitializeCommand.ExecuteAsync(null);
        }
    }
}
