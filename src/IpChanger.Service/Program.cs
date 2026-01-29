using IpChanger.Service;

// Reduce thread pool for lower memory footprint (service is mostly idle)
ThreadPool.SetMinThreads(2, 2);
ThreadPool.SetMaxThreads(8, 8);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "IpChangerService";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
