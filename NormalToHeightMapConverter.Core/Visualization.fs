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

        let allValues =
            seq {
                for y = 0 to height - 1 do
                    for x = 0 to width - 1 do
                        yield heightMap.[y, x]
            }

        let validValues =
            allValues
            |> Seq.filter (fun v -> not (Double.IsNaN v || Double.IsInfinity v))
            |> Seq.toArray

        let (minValue, maxValue) =
            if validValues.Length = 0 then
                (0.0, 256.0)
            else
                (Array.min validValues, Array.max validValues)

        let range = maxValue - minValue

        let effectiveRange =
            if Double.IsNaN range || Double.IsInfinity range || abs range < 1e-10 then
                1.0
            else
                range

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

        image.Save(outputPath, PngEncoder(CompressionLevel = PngCompressionLevel.BestCompression))
        printfn "Success!"
