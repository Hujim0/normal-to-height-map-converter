namespace NormalToHeightMapConverter

open System
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing
open SixLabors.ImageSharp.Formats.Png

module Visualization =

    let saveHeightMapAsImage (heightMap: float[,]) (outputPath: string) =
        printfn $"Saving to {outputPath}"
        let height = heightMap.GetLength(0)
        let width = heightMap.GetLength(1)

        // Collect all values into a sequence
        let allValues =
            seq {
                for y = 0 to height - 1 do
                    for x = 0 to width - 1 do
                        yield heightMap.[y, x]
            }

        // Filter valid values (non-NaN, non-infinity)
        let validValues =
            allValues
            |> Seq.filter (fun v -> not (Double.IsNaN v || Double.IsInfinity v))
            |> Seq.toArray

        // Handle case with no valid values
        let (minValue, maxValue) =
            if validValues.Length = 0 then
                (0.0, 0.0) // Default when no valid values exist
            else
                (Array.min validValues, Array.max validValues)

        // Calculate effective range with safeguards
        let range = maxValue - minValue

        let effectiveRange =
            if Double.IsNaN range || Double.IsInfinity range || abs range < 1e-10 then
                1.0
            else
                range

        // Create image with proper normalization
        use image = new Image<L8>(width, height)

        for y = 0 to height - 1 do
            for x = 0 to width - 1 do
                let value = heightMap.[y, x]

                let normalized =
                    match Double.IsNaN value, Double.IsInfinity value with
                    | true, _
                    | _, true -> 0.0
                    | false, false -> (value - minValue) / effectiveRange |> max 0.0 |> min 1.0

                image.[x, y] <- L8(byte (normalized * 255.0))

        // Save with optimal compression
        image.Save(outputPath, PngEncoder(CompressionLevel = PngCompressionLevel.BestCompression))
        printfn "Success!"
