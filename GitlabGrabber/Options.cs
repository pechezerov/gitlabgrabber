using CommandLine;

public class Options
{
    [Option('o', "output", Required = true, HelpText = "Path to directory to save data")]
    public string OutputPath { get; set; } = Environment.CurrentDirectory;

    [Option('h', "host", Required = false, HelpText = "Url of gitlab server (default is https://gitlab.com)")]
    public string GitlabHost { get; set; } = "https://gitlab.com";

    [Option('t', "token", Required = true, HelpText = "Gitlab token")]
    public string Token { get; set; } = default!;
}
