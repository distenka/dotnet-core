using Microsoft.Extensions.Hosting;
using Distenka;

await ProcessorHost.CreateDefaultBuilder(args)
    .Build()
    .RunAsync();