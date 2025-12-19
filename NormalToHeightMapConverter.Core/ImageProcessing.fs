namespace NormalToHeightMapConverter

module ImageProcessing =
    open OpenCvSharp

    let loadNormalMap (filePath: string) : Mat =
        use image = new Mat(filePath, ImreadModes.Color)

        if image.Empty() then
            failwithf "Could not load image from %s" filePath

        image.Clone()

    let loadNormalMapWithAlpha (filePath: string) : Mat =
        use image = new Mat(filePath, ImreadModes.AnyColor)

        if image.Empty() then
            failwithf "Could not load image from %s" filePath

        image.Clone()
