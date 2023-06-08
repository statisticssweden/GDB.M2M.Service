using System;
using System.Net.Http;
using GDB.M2M.Service.Configurations;
using GDB.M2M.Service.HttpClients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GDB.M2M.Service
{
    class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    Console.WriteLine($"Environment is: {hostContext.HostingEnvironment.EnvironmentName}.");

                    services.AddTransient<IFileInfoFactory, FileInfoFactory>();
                    services.AddTransient<IFileWatcher, FileWatcher>();
                    services.AddTransient<ICertificateStore, CertificateStore>();
                    services.AddTransient<IRequestInfoGenerator, RequestInfoGenerator>();
                    services.AddTransient<IFileReadyChecker, FileReadyChecker>();

                    // You can either use RestSharp or .NET built in HttpClient.
                    // Default is all false which will use NetHttpClientV2.
                    bool useRestSharp = false;
                    bool useRestSharpV2 = false;
                    bool useNetHttpClient = false;

                    if (useRestSharp) // Deprecated will be removed 2024
                    {
                        services.AddTransient<IM2MHttpClient, RestSharpFileUploader>();
                    }
                    else if (useRestSharpV2) // RestSharpV2 Replaces RestSharp
                    {
                        services.AddTransient<IM2MHttpClient, RestSharpFileUploaderV2>();
                    }
                    else if (useNetHttpClient) // Deprecated will be removed 2024
                    {
                        services.AddTransient<IM2MHttpClient, NetHttpClient>();

                        services.AddHttpClient(nameof(NetHttpClient))
                            .ConfigurePrimaryHttpMessageHandler(provider =>
                                {
                                    var certificateStore = provider.GetRequiredService<ICertificateStore>();
                                    var certificate = certificateStore.GetCertificate();
                                    var handler = new HttpClientHandler();
                                    handler.ClientCertificates.Add(certificate);
                                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;

                                    // For testing POST on non-SSL. It can be useful if doing local proxy-testing.
                                    // However, remember to always use proper certificate validation in production environment!
                                    //
                                    //handler.ServerCertificateCustomValidationCallback =
                                    //    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                                    return handler;
                                }
                            )
                            .ConfigureHttpClient((sp, httpClient) =>
                            {
                                var options = sp.GetRequiredService<IOptions<M2MConfiguration>>().Value;
                                httpClient.BaseAddress = new Uri(options.BaseUrl);
                            });
                    }
                    else // NetHttpClientV2 replaces NetHttpClient
                    {
                        services.AddTransient<IM2MHttpClient, NetHttpClientV2>();

                        services.AddHttpClient(nameof(NetHttpClientV2))
                            .ConfigurePrimaryHttpMessageHandler(provider =>
                                {
                                    var certificateStore = provider.GetRequiredService<ICertificateStore>();
                                    var certificate = certificateStore.GetCertificate();
                                    var handler = new HttpClientHandler();
                                    handler.ClientCertificates.Add(certificate);
                                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;

                                    // For testing POST on non-SSL. It can be useful if doing local proxy-testing.
                                    // However, remember to always use proper certificate validation in production environment!
                                    //
                                    //handler.ServerCertificateCustomValidationCallback =
                                    //    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                                    return handler;
                                }
                            )
                            .ConfigureHttpClient((sp, httpClient) =>
                            {
                                var options = sp.GetRequiredService<IOptions<M2MConfiguration>>().Value;
                                httpClient.BaseAddress = new Uri(options.BaseUrl);
                            });
                    }


                    services.Configure<M2MConfiguration>(hostContext.Configuration.GetSection("requestConfiguration"));
                    services.AddHostedService<M2MHostService>();
                });
    }
}
