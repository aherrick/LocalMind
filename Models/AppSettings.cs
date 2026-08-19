namespace LocalMind.Models;

public class AppSettings
{
    public AppSettings()
    {
        SystemPrompt = "";
        Theme = "System";
    }

    public string SystemPrompt { get; set; }
    public string Theme { get; set; }
    public bool StartMinimized { get; set; }
}
