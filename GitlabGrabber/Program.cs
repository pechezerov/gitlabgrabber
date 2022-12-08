using CliWrap;
using CommandLine;
using Newtonsoft.Json;
using System.Text;

try
{
    var options = Parser.Default.ParseArguments<Options>(args).Value;

    // 1. get all groups
    Queue<Group> groups;
    try
    {
        var groupsResultSb = new StringBuilder();
        var cmdGroups = await Cli.Wrap("curl")
            .WithArguments($"{options.GitlabHost}/api/v4/groups -H \"PRIVATE-TOKEN: {options.Token}\"")
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(groupsResultSb))
            .ExecuteAsync();
        var groupsStringData = groupsResultSb.ToString();
        groups = new Queue<Group>(JsonConvert.DeserializeObject<IEnumerable<Group>>(groupsStringData) ?? Enumerable.Empty<Group>());

        if (!groups.Any())
            throw new InvalidOperationException("No groups");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}, skipping...");
        groups = new Queue<Group>(Enumerable.Empty<Group>());
    }

    while (groups.Any())
    {
        // 2. recursively get all subgroups
        var group = groups.Dequeue();
        IEnumerable<Group> subgroups;
        try
        {
            Console.WriteLine($"Collecting {group.name} subgroups...");

            var subGroupsResultSb = new StringBuilder();
            var cmdSubGroups = await Cli.Wrap("curl")
                .WithArguments($"{options.GitlabHost}/api/v4/groups/{group.id}/subgroups -H \"PRIVATE-TOKEN: {options.Token}\"")
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(subGroupsResultSb))
                .ExecuteAsync();
            var subGroupsStringData = subGroupsResultSb.ToString();
            subgroups = JsonConvert.DeserializeObject<IEnumerable<Group>>(subGroupsStringData) ?? Enumerable.Empty<Group>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            subgroups = Enumerable.Empty<Group>();
        }

        foreach (var subgroup in subgroups)
            groups.Enqueue(subgroup);

        // 3. get all projects
        Console.WriteLine($"Grabbing {group.name}...");

        var groupResultSb = new StringBuilder();
        var cmdGroup = await Cli.Wrap("curl")
            .WithArguments($"{options.GitlabHost}/api/v4/groups/{group.id} -H \"PRIVATE-TOKEN: {options.Token}\"")
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(groupResultSb))
            .ExecuteAsync();

        var groupExtended = JsonConvert.DeserializeObject<GroupExtended>(groupResultSb.ToString());
        if (groupExtended.projects is null)
        {
            Console.WriteLine($"No projects in group {group.name}");
        }
        else
        {
            // 4. grab all projects
            foreach (var project in groupExtended.projects)
            {
                try
                {
                    var dirs = group.full_name.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var path = Path.Combine(new[] { options.OutputPath }.Union(dirs).ToArray());
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);

                    Console.WriteLine($"Grabbing {project.name} to {path}");

                    var cmdProjectArchive = await Cli.Wrap("curl")
                        .WithArguments($"{options.GitlabHost}/api/v4/projects/{project.id}/repository/archive.zip -H \"PRIVATE-TOKEN: {options.Token}\" --output \"{path}\\{project.name}.zip\"")
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
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
Console.ReadLine();
