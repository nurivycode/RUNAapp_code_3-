namespace RUNAapp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        // Register routes for navigation
        // These allow navigation using Shell.Current.GoToAsync("//RouteName")
        Routing.RegisterRoute("Welcome", typeof(Views.WelcomePage));
        Routing.RegisterRoute("Login", typeof(Views.LoginPage));
        Routing.RegisterRoute("Register", typeof(Views.RegisterPage));
        Routing.RegisterRoute("Dashboard", typeof(Views.DashboardPage));
        Routing.RegisterRoute("Navigation", typeof(Views.NavigationPage));
        Routing.RegisterRoute("Vision", typeof(Views.VisionPage));
        Routing.RegisterRoute("Setup", typeof(Views.SetupPage));
    }
}
