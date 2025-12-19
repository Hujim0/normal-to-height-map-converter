namespace NormalToHeightMapConverter

open System
open System.Drawing
open System.Drawing.Imaging

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

        use bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed)
        let palette = bitmap.Palette

        for i = 0 to 255 do
            palette.Entries.[i] <- Color.FromArgb(i, i, i)

        bitmap.Palette <- palette

        let range = maxValue - minValue
        let range = if range < 1e-10 then 1.0 else range

        for y = 0 to height - 1 do
            for x = 0 to width - 1 do
                let value = heightMap.[y, x]

                let normalized =
                    if Double.IsNaN value || Double.IsInfinity value then
                        0.0
                    else
                        (value - minValue) / range

                let byteValue = byte (max 0.0 (min 1.0 normalized) * 255.0)
                bitmap.SetPixel(x, y, Color.FromArgb(int byteValue, int byteValue, int byteValue))

        bitmap.Save(outputPath, ImageFormat.Png)
