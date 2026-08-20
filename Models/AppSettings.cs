namespace LocalMind.Models;

public class AppSettings
{
    public AppSettings()
    {
        SystemPrompt = "";
        Theme = "System";
        MinimizeToTrayOnClose = true;
    }

    public string SystemPrompt { get; set; }
    public string Theme { get; set; }
    public bool StartMinimized { get; set; }
    public bool MinimizeToTrayOnClose { get; set; }
}
