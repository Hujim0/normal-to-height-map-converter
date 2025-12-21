namespace NormalToHeightMapConverter.Web

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =
        let builder = WebApplication.CreateBuilder(args)
        builder.Services.AddControllers() |> ignore

        // Resolve upload path with validation
        let uploadPath =
            match Environment.GetEnvironmentVariable("UPLOAD_PATH") with
            | null
            | "" ->
                let basePath = Directory.GetCurrentDirectory()
                Path.Combine(basePath, "uploads")
            | path -> path

        if String.IsNullOrWhiteSpace(uploadPath) then
            failwith "UPLOAD_PATH environment variable is not configured"

        // Create and validate directory at startup
        if not (Directory.Exists(uploadPath)) then
            Directory.CreateDirectory(uploadPath) |> ignore

        // Register settings as singleton
        let appSettings = { UploadPath = uploadPath }
        builder.Services.AddSingleton appSettings |> ignore

        // Register controller without factory
        builder.Services.AddScoped<UploadController>() |> ignore

        let app = builder.Build()
        app.MapControllers() |> ignore
        app.Run()
        exitCode
