open System.IO
open Argu
open NormalToHeightMapConverter.Core.Reconstruct
open NormalToHeightMapConverter.CLI

type Arguments =
    | Input of path: string
    | Output of path: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Input _ -> "Specifies the input file path"
            | Output _ -> "Specifies the output file path"


let eta0 = 0.25
let eps = 1e-6
let tau = 1000.0

let processNormals (normals: Normal array2d) (seed: Point * float) outputFile =
    let H = normals.GetLength 0
    let W = normals.GetLength 1

    let maxIter = max H W * 1000

    let res = reconstructHeightFromNormals normals seed eta0 tau maxIter eps

    ImageProcessing.saveHeightMap res outputFile
    printfn "Processing completed successfully!"


[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<Arguments>(programName = "FileProcessor")

    try
        let results = parser.ParseCommandLine argv

        let inputFile = results.GetResult(<@ Input @>)
        let outputFile = results.GetResult(<@ Output @>)

        printfn $"Reading from: {inputFile}"
        printfn $"Writing to: {outputFile}"

        // ImageProcessing.printImagePixels inputFile
        let maybeNormals = ImageProcessing.loadNormalMap inputFile

        match maybeNormals with
        | None -> 0
        | Some normals ->
            let W = normals.GetLength 0
            let H = normals.GetLength 1
            processNormals normals ({ X = 0; Y = 0 }, 0.0) $"{outputFile}_left_up.png"
            processNormals normals ({ X = W - 1; Y = 0 }, 0.0) $"{outputFile}_right_up.png"
            processNormals normals ({ X = 0; Y = H - 1 }, 0.0) $"{outputFile}_left_down.png"
            processNormals normals ({ X = W - 1; Y = H - 1 }, 0.0) $"{outputFile}_right_down.png"
            0

    with
    | :? ArguParseException as ex ->
        printfn $"Error: {ex.Message}"
        1
    | :? FileNotFoundException as ex ->
        printfn $"Error: Input file not found - {ex.FileName}"
        1
    | ex ->
        printfn $"Unexpected error: {ex.Message}"
        1
