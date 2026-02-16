using RUNAapp.ViewModels;

namespace RUNAapp.Views;

public partial class AccessCodePage : ContentPage
{
    public AccessCodePage(AccessCodeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
