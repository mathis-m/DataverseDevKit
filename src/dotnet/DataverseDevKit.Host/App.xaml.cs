using Microsoft.Extensions.DependencyInjection;

namespace DataverseDevKit.Host
{
  public partial class App : Application
  {
    public App()
    {
      InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
      return new Window(new AppShell());
    }
  }
}