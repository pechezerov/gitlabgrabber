using CliWrap;
using Newtonsoft.Json;
using System.Text;

if (args.Count() < 3)
{
    Console.WriteLine("Usage:GitlabGrabber.exe targetlocation host token");
    Console.WriteLine("Example: C:\\data\\gitlab https:\\gitlab.com glpat-zzzzxzxzxxxxzxzx");
    return;
}

string targetPath = args[0].Replace("\"", "");
string host = args[1];
string token = args[2];

// 1. get all groups
var groupsResultSb = new StringBuilder();
var cmdGroups = await Cli.Wrap("curl")
    .WithArguments($"{host}/api/v4/groups -H \"PRIVATE-TOKEN: {token}\"")
    .WithValidation(CommandResultValidation.None)
    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(groupsResultSb))
    .ExecuteAsync();
var groupsStringData = groupsResultSb.ToString();

try
{
    var groups = JsonConvert.DeserializeObject<IEnumerable<Group>>(groupsStringData);

    // 2. get all projects
    foreach (var group in groups)
    {
        Console.WriteLine($"Grabbing {group.name}...");

        var groupResultSb = new StringBuilder();
        var cmdGroup = await Cli.Wrap("curl")
            .WithArguments($"{host}/api/v4/groups/{group.id} -H \"PRIVATE-TOKEN: {token}\"")
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(groupResultSb))
            .ExecuteAsync();

        var groupExtended = JsonConvert.DeserializeObject<GroupExtended>(groupResultSb.ToString());
        if (groupExtended.projects is null)
        {
            Console.WriteLine($"No projects");
        }
        else
        {
            // 3. grab all projects
            foreach (var project in groupExtended.projects)
            {
                try
                {
                    var dirs = project.name_with_namespace.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var path = Path.Combine(new[] { targetPath }.Union(dirs).ToArray());
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);

                    Console.WriteLine($"Grabbing {project.name} to {path}");

                    var cmdProjectArchive = await Cli.Wrap("curl")
                        .WithArguments($"{host}/api/v4/projects/{project.id}/repository/archive.zip -H \"PRIVATE-TOKEN: {token}\" --output \"{path}\\{project.name}.zip\"")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteAsync();

                    if (cmdProjectArchive.ExitCode != 0)
                        Console.WriteLine($"ExitCode: {cmdProjectArchive.ExitCode}, skipping...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}, skipping...");
                }
            }
        }
    }

    Console.WriteLine($"OK =)");
}
catch
{
    Console.WriteLine($"Error: {groupsStringData}, skipping...");
}

Console.ReadLine();
