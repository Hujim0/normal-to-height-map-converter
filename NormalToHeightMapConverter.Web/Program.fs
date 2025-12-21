namespace NormalToHeightMapConverter.Web

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
// Program.fs
module Program =
    open NormalToHeightMapConverter.Web.Services

    [<EntryPoint>]
    let main args =
        let builder = WebApplication.CreateBuilder(args)
        builder.Services.AddControllers() |> ignore

        // Configure upload path
        let uploadPath =
            match Environment.GetEnvironmentVariable("UPLOAD_PATH") with
            | null
            | "" -> Path.Combine(Directory.GetCurrentDirectory(), "uploads")
            | path -> path

        if not (Directory.Exists(uploadPath)) then
            Directory.CreateDirectory(uploadPath) |> ignore

        // Bind height map settings with CLI defaults as fallbacks
        let config = builder.Configuration
        let heightMapSection = config.GetSection("HeightMapSettings")

        let heightMapConfig =
            { Eta0 =
                if heightMapSection["Eta0"] |> isNull then
                    None
                else
                    Some(float heightMapSection["Eta0"])
              Tau =
                if heightMapSection["Tau"] |> isNull then
                    None
                else
                    Some(float heightMapSection["Tau"])
              Epsilon =
                if heightMapSection["Epsilon"] |> isNull then
                    None
                else
                    Some(float heightMapSection["Epsilon"])
              Seeds =
                if heightMapSection["Seeds"] |> isNull then
                    None
                else
                    Some(int heightMapSection["Seeds"])
              Combine = defaultArg (heightMapSection["Combine"] |> Option.ofObj) "average" }

        // Create full app settings
        let appSettings =
            { UploadPath = uploadPath
              HeightMap = heightMapConfig }

        // Register services
        builder.Services.AddSingleton(appSettings) |> ignore
        builder.Services.AddScoped<IHeightMapService, HeightMapService>() |> ignore

        let app = builder.Build()
        app.MapControllers() |> ignore
        app.Run()
        0
