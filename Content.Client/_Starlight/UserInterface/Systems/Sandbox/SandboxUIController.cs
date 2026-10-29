// ReSharper disable CheckNamespace
using Content.Client.UserInterface.Systems.Sandbox.Windows;
using Content.Client.Weather;

namespace Content.Client.UserInterface.Systems.Sandbox;

public sealed partial class SandboxUIController
{
    private void ConfigureWeatherToggle(SandboxWindow window)
    {
        window.OnOpen += () => window.ToggleWeatherButton.Pressed = !EntityManager.System<WeatherSystem>().DrawWeather;
        window.ToggleWeatherButton.OnToggled += args => EntityManager.System<WeatherSystem>().DrawWeather = !args.Pressed;
    }
}
