using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisualVoicemailPro;

/// <summary>
/// Visual Voicemail Pro App - Entry Point
/// </summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Set the main page
        MainPage = new AppShell();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);

        window.Title = "Visual Voicemail Pro";
        
        // Set minimum window size for desktop
        window.MinimumHeight = 600;
        window.MinimumWidth = 400;

        return window;
    }
}