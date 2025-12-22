// HeightMapService.fs
namespace NormalToHeightMapConverter.Web.Services

open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open NormalToHeightMapConverter.Integration
open NormalToHeightMapConverter.Visualization
open NormalToHeightMapConverter.Web
open NormalToHeightMapConverter.Meshing
open System.IO



type IHeightMapService =
    abstract member GenerateFromPath:
        inputImagePath: string * outputImagePath: string * settings: HeightMapConfig -> unit

type HeightMapService() =
    let generate3DModel (outputDir: string) (heightMap: float array2d) : unit =
        // Generate the mesh first to get the quads
        let (topQuads, sideFaces) = generateMesh heightMap

        // Write the OBJ file
        writeObj (Path.Combine(outputDir, "model.obj")) (topQuads, sideFaces)

        // Hardcoded MTL file content with proper materials and double-sided rendering
        let mtlContent =
            """
# Terrain materials for Three.js compatibility
# Double-sided rendering to prevent missing faces

newmtl terrain_top
Ka 0.3 0.3 0.3      # Ambient color (dark gray)
Kd 0.7 0.7 0.7      # Diffuse color (light gray for top faces)
Ks 0.0 0.0 0.0      # Specular color (no shininess)
Ns 10.0             # Specular exponent (soft highlights)
illum 2             # Illumination model (diffuse + specular)
d 1.0               # Opacity (fully opaque)
side 2              # CRITICAL: Double-sided rendering - fixes missing faces in Three.js

newmtl terrain_side
Ka 0.2 0.2 0.2      # Ambient color (darker for sides)
Kd 0.4 0.4 0.4      # Diffuse color (medium gray for side faces)
Ks 0.0 0.0 0.0      # Specular color (no shininess)
Ns 10.0             # Specular exponent
illum 2             # Illumination model
d 1.0               # Opacity
side 2              # CRITICAL: Double-sided rendering
"""

        // Write the proper MTL file
        File.WriteAllText(Path.Combine(outputDir, "model.mtl"), mtlContent)


    interface IHeightMapService with
        member this.GenerateFromPath(inputImagePath, outputFolder, settings) =
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

            saveHeightMapAsImage heightMap (Path.Combine(outputFolder, "height_map.png"))

            generate3DModel outputFolder heightMap
