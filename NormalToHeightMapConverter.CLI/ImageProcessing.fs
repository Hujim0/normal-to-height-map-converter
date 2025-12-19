namespace NormalToHeightMapConverter.CLI

open System
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats

module ImageProcessing =
    open NormalToHeightMapConverter.Core.Reconstruct

    let loadNormalMap (filePath: string) : Normal array2d option =
        try
            use image = Image.Load<Rgb24>(filePath)
            let width = image.Width
            let height = image.Height
            let normals = Array2D.zeroCreate<Normal> height width

            let epsilon = 0.01
            let minValidMagnitude = 1e-5

            for y in 0 .. height - 1 do
                for x in 0 .. width - 1 do
                    let pixel = image.[x, y]
                    // Convert [0,255] to [-1,1] range
                    let vx = (float pixel.R / 255.0) * 2.0 - 1.0
                    let vy = (float pixel.G / 255.0) * 2.0 - 1.0
                    let vz = (float pixel.B / 255.0) * 2.0 - 1.0

                    let magnitude = sqrt (vx * vx + vy * vy + vz * vz)

                    // Handle near-zero vectors
                    if magnitude < minValidMagnitude then
                        normals.[y, x] <- { Nx = 0.0; Ny = 0.0; Nz = 1.0 }
                    else
                        // Check normalization deviation
                        let deviation = abs magnitude - 1.0

                        if deviation > epsilon then
                            printfn
                                $"WARNING: Image contains non-normalized vectors (deviation: {deviation} > {epsilon}) at {x} {y}. Data may be invalid."

                        // Normalize the vector
                        let nx = vx / magnitude
                        let ny = vy / magnitude
                        let nz = vz / magnitude
                        normals.[y, x] <- { Nx = nx; Ny = ny; Nz = nz }

            Some normals

        with
        | :? System.IO.FileNotFoundException ->
            printfn $"Error: File not found at path: {filePath}"
            None
        | :? SixLabors.ImageSharp.UnknownImageFormatException ->
            printfn $"Error: Unsupported image format at path: {filePath}"
            None
        | ex ->
            printfn $"An error occurred: {ex.Message}"
            None

    let printImagePixels (filePath: string) =
        try
            use image = Image.Load<Rgb24>(filePath)

            printfn $"Image dimensions: {image.Width}x{image.Height}"
            printfn "Printing pixels (row by row, left to right)..."

            for y in 0 .. image.Height - 1 do
                for x in 0 .. image.Width - 1 do
                    let pixel = image.[x, y]
                    printfn $"Pixel at ({x}, {y}): R={pixel.R} G={pixel.G} B={pixel.B}"

            printfn "Finished processing all pixels."

        with
        | :? System.IO.FileNotFoundException -> printfn $"Error: File not found at path: {filePath}"
        | :? SixLabors.ImageSharp.UnknownImageFormatException ->
            printfn $"Error: Unsupported image format at path: {filePath}"
        | ex -> printfn $"An error occurred: {ex.Message}"


    let saveHeightMap (heightMap: float[,]) (outputPath: string) =
        let H = heightMap.GetLength(0)
        let W = heightMap.GetLength(1)

        // Find valid min/max values (ignoring NaNs)
        let mutable minVal = Double.PositiveInfinity
        let mutable maxVal = Double.NegativeInfinity
        let mutable validCount = 0

        for i in 0 .. H - 1 do
            for j in 0 .. W - 1 do
                let v = heightMap.[i, j]

                if not (Double.IsNaN v) then
                    validCount <- validCount + 1

                    if v < minVal then
                        minVal <- v

                    if v > maxVal then
                        maxVal <- v

        if validCount = 0 then
            failwith "Height map contains only invalid (NaN) values"

        // Handle constant height maps
        let range =
            let r = maxVal - minVal
            if r < 1e-10 then 1.0 else r

        use image = new Image<L8>(W, H) // Efficient 8-bit grayscale format

        for i in 0 .. H - 1 do
            for j in 0 .. W - 1 do
                let v = heightMap.[i, j]

                let normalized =
                    if Double.IsNaN v then
                        0.0
                    else
                        let n = (v - minVal) / range
                        // Apply non-linear stretch for better visualization
                        min 1.0 (max 0.0 (n))

                let byteVal = byte (normalized * 255.0 + 0.5)
                image.[j, i] <- L8 byteVal // (x=j, y=i) - column/row mapping

        // Add metadata to indicate this is a height map
        // image.Metadata.GetValue<string>("Description") <- "Normal-to-height reconstruction"

        image.Save outputPath
