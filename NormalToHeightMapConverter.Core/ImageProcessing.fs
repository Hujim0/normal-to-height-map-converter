namespace NormalToHeightMapConverter

module ImageProcessing =
    open SixLabors.ImageSharp
    open SixLabors.ImageSharp.PixelFormats
    open SixLabors.ImageSharp.Processing
    open System.IO

    /// Loads a normal map image without alpha channel
    let loadNormalMap (filePath: string) : Image<Rgba32> =
        try
            use stream = File.OpenRead(filePath)
            let image = Image.Load<Rgba32>(stream)
            image
        with ex ->
            failwithf "Could not load image from %s: %s" filePath ex.Message

    /// Loads a normal map image with alpha channel preserved
    let loadNormalMapWithAlpha (filePath: string) : Image<Rgba32> =
        try
            use stream = File.OpenRead(filePath)
            let image = Image.Load<Rgba32>(stream)
            image
        with ex ->
            failwithf "Could not load image from %s: %s" filePath ex.Message
