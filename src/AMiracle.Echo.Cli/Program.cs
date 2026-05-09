using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var serverUrl = Environment.GetEnvironmentVariable("ECHO_SERVER_URL") ?? "http://localhost:8080";
var token = Environment.GetEnvironmentVariable("ECHO_ADMIN_TOKEN") ?? "";

var positional = new List<string>();
var flags = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
for (int i = 0; i < args.Length; i++)
{
    var a = args[i];
    if (a.StartsWith("--"))
    {
        var key = a[2..];
        var val = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : null;
        flags[key] = val;
    }
    else positional.Add(a);
}

if (flags.TryGetValue("server", out var s) && s is not null) serverUrl = s;
if (flags.TryGetValue("token", out var t) && t is not null) token = t;

if (positional.Count == 0)
{
    PrintUsage();
    return 0;
}

if (string.IsNullOrEmpty(token))
{
    Console.Error.WriteLine("ECHO_ADMIN_TOKEN env var or --token flag required.");
    return 2;
}

var http = new HttpClient { BaseAddress = new Uri(serverUrl.TrimEnd('/') + "/api/v1/") };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

try
{
    return positional[0].ToLowerInvariant() switch
    {
        "projects" => await HandleProjects(positional.Skip(1).ToList(), flags, http),
        "feedbacks" => await HandleFeedbacks(positional.Skip(1).ToList(), flags, http),
        "submitters" => await HandleSubmitters(positional.Skip(1).ToList(), http),
        "help" or "-h" or "--help" => PrintUsage(),
        _ => Unknown(positional[0]),
    };
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Request failed: {ex.Message}");
    return 1;
}

static int Unknown(string verb) { Console.Error.WriteLine($"Unknown command: {verb}"); return PrintUsage(); }

static int PrintUsage()
{
    Console.WriteLine("""
amiracle-echo  —  manage AMiracle.Echo projects and feedbacks

Connection:  set ECHO_SERVER_URL and ECHO_ADMIN_TOKEN, or pass --server / --token

Commands:
  projects list
  projects create --name <name> [--origins a,b] [--retention-days N]
  projects show <id>
  projects rotate-key <id>
  projects delete <id>

  feedbacks list [--project <id>] [--status new|triaged|resolved] [--type text|voice] [--limit N]
  feedbacks show <id>
  feedbacks set-status <id> <new|triaged|resolved>
  feedbacks set-priority <id> <1..5>
  feedbacks set-category <id> <bug|idea|praise|question>
  feedbacks delete <id>
  feedbacks download-audio <id> --out <path>
  feedbacks download-screenshot <id> --out <path>

  submitters delete <submitterId>
""");
    return 0;
}

static async Task<int> HandleProjects(List<string> args, Dictionary<string, string?> flags, HttpClient http)
{
    if (args.Count == 0) { Console.Error.WriteLine("projects: missing subcommand"); return 2; }
    switch (args[0].ToLowerInvariant())
    {
        case "list":
        {
            using var r = await http.GetAsync("admin/projects");
            await PrintJson(r); return 0;
        }
        case "create":
        {
            if (!flags.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            { Console.Error.WriteLine("--name required"); return 2; }
            var origins = flags.TryGetValue("origins", out var o) && !string.IsNullOrEmpty(o)
                ? o.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray()
                : Array.Empty<string>();
            int? retention = flags.TryGetValue("retention-days", out var rd) && int.TryParse(rd, out var rdi) ? rdi : null;
            var body = new { name, allowedOrigins = origins, retentionDays = retention };
            using var r = await http.PostAsJsonAsync("admin/projects", body);
            await PrintJson(r); return 0;
        }
        case "show": return await GetById(http, "admin/projects", args);
        case "rotate-key":
        {
            if (args.Count < 2) { Console.Error.WriteLine("project id required"); return 2; }
            using var r = await http.PostAsync($"admin/projects/{args[1]}/rotate-key", null);
            await PrintJson(r); return 0;
        }
        case "delete": return await DeleteById(http, "admin/projects", args);
        default: Console.Error.WriteLine($"projects: unknown subcommand {args[0]}"); return 2;
    }
}

static async Task<int> HandleFeedbacks(List<string> args, Dictionary<string, string?> flags, HttpClient http)
{
    if (args.Count == 0) { Console.Error.WriteLine("feedbacks: missing subcommand"); return 2; }
    switch (args[0].ToLowerInvariant())
    {
        case "list":
        {
            var qs = new List<string>();
            if (flags.TryGetValue("project", out var p) && !string.IsNullOrEmpty(p)) qs.Add($"projectId={p}");
            if (flags.TryGetValue("status", out var st) && !string.IsNullOrEmpty(st)) qs.Add($"status={st}");
            if (flags.TryGetValue("type", out var ty) && !string.IsNullOrEmpty(ty)) qs.Add($"type={ty}");
            if (flags.TryGetValue("limit", out var lim) && !string.IsNullOrEmpty(lim)) qs.Add($"limit={lim}");
            using var r = await http.GetAsync("admin/feedbacks?" + string.Join("&", qs));
            await PrintJson(r); return 0;
        }
        case "show": return await GetById(http, "admin/feedbacks", args);
        case "set-status":
        {
            if (args.Count < 3) { Console.Error.WriteLine("usage: feedbacks set-status <id> <status>"); return 2; }
            using var r = await http.PatchAsJsonAsync($"admin/feedbacks/{args[1]}", new { status = args[2] });
            await PrintJson(r); return 0;
        }
        case "set-priority":
        {
            if (args.Count < 3 || !short.TryParse(args[2], out var pri)) { Console.Error.WriteLine("usage: feedbacks set-priority <id> <1..5>"); return 2; }
            using var r = await http.PatchAsJsonAsync($"admin/feedbacks/{args[1]}", new { priority = pri });
            await PrintJson(r); return 0;
        }
        case "set-category":
        {
            if (args.Count < 3) { Console.Error.WriteLine("usage: feedbacks set-category <id> <category>"); return 2; }
            using var r = await http.PatchAsJsonAsync($"admin/feedbacks/{args[1]}", new { category = args[2] });
            await PrintJson(r); return 0;
        }
        case "delete": return await DeleteById(http, "admin/feedbacks", args);
        case "download-audio": return await DownloadBlob(http, args, flags, "audio");
        case "download-screenshot": return await DownloadBlob(http, args, flags, "screenshot");
        default: Console.Error.WriteLine($"feedbacks: unknown subcommand {args[0]}"); return 2;
    }
}

static async Task<int> HandleSubmitters(List<string> args, HttpClient http)
{
    if (args.Count >= 2 && args[0].Equals("delete", StringComparison.OrdinalIgnoreCase))
    {
        using var r = await http.DeleteAsync($"admin/submitters/{Uri.EscapeDataString(args[1])}");
        await PrintJson(r); return 0;
    }
    Console.Error.WriteLine("usage: submitters delete <submitterId>");
    return 2;
}

static async Task<int> GetById(HttpClient http, string root, List<string> args)
{
    if (args.Count < 2) { Console.Error.WriteLine("id required"); return 2; }
    using var r = await http.GetAsync($"{root}/{args[1]}");
    await PrintJson(r); return 0;
}

static async Task<int> DeleteById(HttpClient http, string root, List<string> args)
{
    if (args.Count < 2) { Console.Error.WriteLine("id required"); return 2; }
    using var r = await http.DeleteAsync($"{root}/{args[1]}");
    if (!r.IsSuccessStatusCode) { Console.Error.WriteLine($"{(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}"); return 1; }
    Console.WriteLine("deleted");
    return 0;
}

static async Task<int> DownloadBlob(HttpClient http, List<string> args, Dictionary<string, string?> flags, string kind)
{
    if (args.Count < 2 || !flags.TryGetValue("out", out var path) || string.IsNullOrEmpty(path))
    { Console.Error.WriteLine($"usage: feedbacks download-{kind} <id> --out <path>"); return 2; }
    using var r = await http.GetAsync($"admin/feedbacks/{args[1]}/{kind}");
    if (!r.IsSuccessStatusCode) { Console.Error.WriteLine($"{(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}"); return 1; }
    await using var fs = File.Create(path);
    await (await r.Content.ReadAsStreamAsync()).CopyToAsync(fs);
    Console.WriteLine($"saved {path}");
    return 0;
}

static async Task PrintJson(HttpResponseMessage r)
{
    var txt = await r.Content.ReadAsStringAsync();
    if (!r.IsSuccessStatusCode) { Console.Error.WriteLine($"{(int)r.StatusCode} {r.ReasonPhrase}"); Console.Error.WriteLine(txt); Environment.Exit(1); }
    if (string.IsNullOrWhiteSpace(txt)) return;
    try
    {
        using var doc = JsonDocument.Parse(txt);
        Console.WriteLine(JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true }));
    }
    catch { Console.WriteLine(txt); }
}
