namespace NormalToHeightMapConverter.CLI

open Argu

type Arguments =
    | [<AltCommandLine "-i">] Input of path: string
    | [<AltCommandLine "-o">] Output of path: string
    | [<AltCommandLine "-n">] Iterations of count: int
    | [<AltCommandLine "-e">] Eta0 of value: float
    | [<AltCommandLine "-t">] Tau of value: float
    | [<AltCommandLine "--eps">] Epsilon of value: float
    | [<AltCommandLine "-s">] Seeds of count: int
    | [<AltCommandLine "-c">] Combine of method: string // New combination method parameter

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Input _ -> "Input normal map image path"
            | Output _ -> "Output height map image path"
            | Iterations _ -> "Number of iterations (default: auto-calculated)"
            | Eta0 _ -> "Initial step size (default: 0.05)"
            | Tau _ -> "Learning rate decay factor (default: 100.0)"
            | Epsilon _ -> "Convergence threshold (default: 1e-5)"
            | Seeds _ -> "Number of seed points on border for reconstruction (default: 4)"
            | Combine _ -> "Height map combination method: 'average' (default) or 'min'"

module Main =
    open NormalToHeightMapConverter.Integration
    open NormalToHeightMapConverter.ImageProcessing
    open NormalToHeightMapConverter.Visualization
    open SixLabors.ImageSharp
    open SixLabors.ImageSharp.PixelFormats

    [<EntryPoint>]
    let main argv =
        let parser = ArgumentParser.Create<Arguments>(programName = "Normal2Height")

        try
            let results = parser.ParseCommandLine argv

            let inputFile = results.GetResult(<@ Input @>, "input.png")
            let outputFile = results.GetResult(<@ Output @>, "output.png")
            let iterations = results.TryGetResult(<@ Iterations @>)
            let eta0 = results.TryGetResult(<@ Eta0 @>)
            let tau = results.TryGetResult(<@ Tau @>)
            let eps = results.TryGetResult(<@ Epsilon @>)
            let seeds = results.TryGetResult(<@ Seeds @>)
            let combineMethod = results.TryGetResult(<@ Combine @>) // Get combination method

            printfn $"Processing: {inputFile} -> {outputFile}"

            printfn
                $"Iterations: {defaultArg
                                   iterations
                                   (calculateMinIterations
                                       (Image.Load<Rgba32>(inputFile).Width)
                                       (Image.Load<Rgba32>(inputFile).Height)
                                       (defaultArg seeds 4))}"

            match eta0 with
            | Some v -> printfn $"Eta0: {v}"
            | None -> ()

            match tau with
            | Some v -> printfn $"Tau: {v}"
            | None -> ()

            match eps with
            | Some v -> printfn $"Epsilon: {v}"
            | None -> ()

            match seeds with
            | Some v -> printfn $"Border seeds: {v}"
            | None -> printfn "Border seeds: 4 (default)"

            match combineMethod with
            | Some v -> printfn $"Combination method: {v}"
            | None -> printfn "Combination method: average (default)"

            printfn "Loading normal map..."
            let normalMap = loadNormalMap inputFile

            printfn "Estimating height map..."
            // Pass seeds and combination method parameters
            let heightMap =
                estimateHeightMap normalMap eta0 tau iterations eps seeds combineMethod

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
