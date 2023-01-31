using CliWrap;
using CommandLine;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

try
{
    var options = Parser.Default.ParseArguments<Options>(args).Value;
    var fileNameRegexFilter = new Regex("[^A-Za-z0-9а-яА-Яё!@#$^&№;%:?*()-=_+ ]", RegexOptions.Compiled);

    // 1. get all groups
    if (options.LoadGroups != 0)
    {
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
            try
            {
                groups = new Queue<Group>(JsonConvert.DeserializeObject<IEnumerable<Group>>(groupsStringData) ?? Enumerable.Empty<Group>());
            }
            catch
            {
                var err = JsonConvert.DeserializeObject<Error>(groupsStringData);
                throw new InvalidOperationException($"{(err.message != null ? err.message : $"{err.error}: {err.error_description}")}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error: {ex.Message}");
        }

        if (groups.Any())
        {
            // 2. recursively get all subgroups
            while (groups.Any())
            {
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
                if (groupExtended?.projects is null)
                {
                    Console.WriteLine($"No projects in group {group.name}");
                }
                else
                {
                    // 4. grab all projects
                    var projects = groupExtended.projects;
                    await HandleProjects(options, fileNameRegexFilter, group, projects);
                }
            }
            Console.WriteLine($"OK =)");
        }
    }

    if (options.LoadDirectProjects != 0)
    {
        // 5. get all projects
        try
        {
            var groupResultSb = new StringBuilder();
            var cmdProjects = await Cli.Wrap("curl")
                .WithArguments($"{options.GitlabHost}/api/v4/projects -H \"PRIVATE-TOKEN: {options.Token}\"")
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(groupResultSb))
                .ExecuteAsync();
            var x = groupResultSb.ToString();
            var projectsSet = JsonConvert.DeserializeObject<Project[]>(x);

            if (projectsSet != null)
                await HandleProjects(options, fileNameRegexFilter, null, projectsSet);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}, skipping...");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

static async Task HandleProjects(Options options, Regex fileNameRegexFilter, Group? group, Project[] projects)
{
    foreach (var project in projects)
    {
        if (fileNameRegexFilter.Matches(project.name).Any())
            project.name = $"project-{project.name.GetHashCode()}";

        if (fileNameRegexFilter.Matches(group?.full_name ?? "").Any())
            group.full_name = $"group-{group?.full_name.GetHashCode()}";

        try
        {
            var dirs = (group?.full_name ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
