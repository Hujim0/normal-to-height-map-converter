namespace NormalToHeightMapConverter

open System
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing
open SixLabors.ImageSharp.Formats.Png

module Visualization =

    let saveHeightMapAsImage (heightMap: float[,]) (outputPath: string) =
        let height = heightMap.GetLength(0)
        let width = heightMap.GetLength(1)

        // Find min and max values for normalization
        let mutable minValue = Double.MaxValue
        let mutable maxValue = Double.MinValue

        for y = 0 to height - 1 do
            for x = 0 to width - 1 do
                let v = heightMap.[y, x]

                if not (Double.IsNaN v || Double.IsInfinity v) then
                    minValue <- min minValue v
                    maxValue <- max maxValue v

        // Handle case where all values are the same
        let range = maxValue - minValue
        let range = if range < 1e-10 then 1.0 else range

        // Create a grayscale image using L8 (8-bit luminance)
        use image = new Image<L8>(width, height)

        for y = 0 to height - 1 do
            for x = 0 to width - 1 do
                let value = heightMap.[y, x]

                let normalized =
                    if Double.IsNaN value || Double.IsInfinity value then
                        0.0
                    else
                        (value - minValue) / range

                let byteValue = byte (max 0.0 (min 1.0 normalized) * 255.0)

                // Set pixel value
                image.[x, y] <- L8(byteValue)

        // Save with optimal PNG compression using initialization syntax
        let encoder = PngEncoder(CompressionLevel = PngCompressionLevel.BestCompression)
        image.Save(outputPath, encoder)
