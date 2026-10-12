namespace Portfolio.Api;

public static class Program
{
    public static Task Main(string[] args) => CreateHostBuilder(args).Build().RunAsync();

    public static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        .UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        })
        .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
}
