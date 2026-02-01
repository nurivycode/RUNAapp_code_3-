using RUNAapp.ViewModels;

namespace RUNAapp.Views;

public partial class SetupPage : ContentPage
{
    public SetupPage(SetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is SetupViewModel vm)
        {
            await vm.InitializeCommand.ExecuteAsync(null);
        }
    }
}
