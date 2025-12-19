open System
open System.IO
open Argu

type Arguments =
    | Input of path: string
    | Output of path: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Input _ -> "Specifies the input file path"
            | Output _ -> "Specifies the output file path"

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<Arguments>(programName = "FileProcessor")

    try
        let results = parser.ParseCommandLine argv

        let inputFile = results.GetResult(<@ Input @>)
        let outputFile = results.GetResult(<@ Output @>)

        printfn $"Reading from: {inputFile}"
        printfn $"Writing to: {outputFile}"

        // Read from input file
        let content = File.ReadAllText(inputFile)

        // Simple processing example (uppercase everything)
        let processedContent = content.ToUpper()

        // Write to output file
        File.WriteAllText(outputFile, processedContent)

        printfn "Processing completed successfully!"
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
