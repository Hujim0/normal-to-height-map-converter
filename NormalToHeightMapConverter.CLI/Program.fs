namespace NormalToHeightMapConverter.CLI


open Argu

type Arguments =
    | [<AltCommandLine "-i">] Input of path: string
    | [<AltCommandLine "-o">] Output of path: string
    | [<AltCommandLine "-n">] Iterations of count: int
    | [<AltCommandLine "-m">] IntegrationType of method: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Input _ -> "Input normal map image path"
            | Output _ -> "Output height map image path"
            | Iterations _ -> "Number of iterations (default: 100)"
            | IntegrationType _ -> "Integration method: sum (default), trapezoid, or simpson"

module Main =
    open NormalToHeightMapConverter.Integration
    open NormalToHeightMapConverter.ImageProcessing
    open NormalToHeightMapConverter.Visualization

    [<EntryPoint>]
    let main argv =
        let parser = ArgumentParser.Create<Arguments>(programName = "Normal2Height")

        try
            let results = parser.ParseCommandLine argv

            let inputFile = results.GetResult(<@ Input @>, "input.png")
            let outputFile = results.GetResult(<@ Output @>, "output.png")
            let iterations = results.GetResult(<@ Iterations @>, 100)
            let integrationArg = results.GetResult(<@ IntegrationType @>, "sum").ToLower()

            let integrationMethod =
                match integrationArg with
                | "trapezoid" -> IntegrationMethod.Trapezoid
                | "simpson" -> IntegrationMethod.Simpson
                | _ -> IntegrationMethod.Sum

            printfn $"Processing: {inputFile} -> {outputFile}"
            printfn $"Iterations: {iterations} | Method: {integrationMethod}"

            printfn "Loading normal map..."
            let normalMap = loadNormalMap inputFile

            printfn "Estimating height map..."
            let heightMap = estimateHeightMap normalMap iterations integrationMethod

            printfn "Saving result..."
            saveHeightMapAsImage heightMap outputFile

            printfn "Success!"
            0
        with
        | :? ArguParseException as ex ->
            printfn $"{parser.PrintUsage()}\n\nError: {ex.Message}"
            1
        | ex ->
            printfn $"Unexpected error: {ex.Message}"
            printfn $"Stack trace: {ex.StackTrace}"
            1
