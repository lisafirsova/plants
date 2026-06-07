namespace Plants;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("plant-detail", typeof(Views.PlantDetailPage));
        Routing.RegisterRoute("issue-detail", typeof(Views.IssueDetailPage));
    }
}
