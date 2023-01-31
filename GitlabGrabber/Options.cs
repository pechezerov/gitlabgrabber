using CommandLine;

public class Options
{
    [Option('o', "output", Required = true, HelpText = "Path to directory to save data")]
    public string OutputPath { get; set; } = Environment.CurrentDirectory;

    [Option('h', "host", Required = false, HelpText = "Url of gitlab server (default is https://gitlab.com)")]
    public string GitlabHost { get; set; } = "https://gitlab.com";

    [Option('t', "token", Required = true, HelpText = "Gitlab token")]
    public string Token { get; set; } = default!;

    [Option('g', "groups", Required = false, HelpText = "Load group projects")]
    public int LoadGroups { get; set; } = 1;

    [Option('p', "projects", Required = false, HelpText = "Load direct projects")]
    public int LoadDirectProjects { get; set; } = 1;
}
