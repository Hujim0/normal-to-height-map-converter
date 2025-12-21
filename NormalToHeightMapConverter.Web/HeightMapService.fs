// HeightMapService.fs
namespace NormalToHeightMapConverter.Web.Services

open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open NormalToHeightMapConverter.Integration
open NormalToHeightMapConverter.Visualization
open NormalToHeightMapConverter.Web


type IHeightMapService =
    abstract member GenerateFromPath:
        inputImagePath: string * outputImagePath: string * settings: HeightMapConfig -> unit

type HeightMapService() =
    interface IHeightMapService with
        member this.GenerateFromPath(inputImagePath, outputImagePath, settings) =
            use image = Image.Load<Rgba32>(inputImagePath)

            let heightMap =
                estimateHeightMap
                    image
                    settings.Eta0
                    settings.Tau
                    None // Auto-calculate iterations
                    settings.Epsilon
                    settings.Seeds
                    (Some settings.Combine)

            saveHeightMapAsImage heightMap outputImagePath
