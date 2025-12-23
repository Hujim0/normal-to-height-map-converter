namespace NormalToHeightMapConverter.Tests

open Xunit
open System
open System.IO
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing
open NormalToHeightMapConverter
open NormalToHeightMapConverter.Reconstruct
open SixLabors.ImageSharp.Drawing.Processing

module IntegrationTests =

    [<Fact>]
    let ``Flat normal map produces flat height map`` () =
        use image = new Image<Rgba32>(2, 2)
        let flatNormal = Rgba32(128uy, 128uy, 255uy, 255uy) // (0,0,1) normalized
        image.Mutate(fun ctx -> ctx.Fill(flatNormal) |> ignore)

        let heightMap =
            Integration.estimateHeightMap image (Some 0.05) (Some 10.0) (Some 5) (Some 1e-3) (Some 1) (Some "average")

        for y in 0..1 do
            for x in 0..1 do
                let value = heightMap.[y, x]
                Assert.True(Math.Abs(value) < 0.01, $"Value at ({x},{y}) = {value}")

    [<Fact>]
    let ``45-degree slope produces expected height differences`` () =
        // Normal for 45° slope: Nx=-0.707, Ny=0, Nz=0.707
        use image = new Image<Rgba32>(3, 1)
        let slopePixel = Rgba32(37uy, 128uy, 218uy, 255uy) // Calculated values
        image.Mutate(fun ctx -> ctx.Fill(slopePixel) |> ignore)

        let heightMap =
            Integration.estimateHeightMap image (Some 0.1) (Some 5.0) (Some 50) (Some 1e-4) (Some 1) (Some "average")

        let height0 = heightMap.[0, 0]
        let height1 = heightMap.[0, 1]
        let height2 = heightMap.[0, 2]

        Assert.True(Math.Abs(height0) < 0.05)
        Assert.True(Math.Abs(height1 - 1.0) < 0.15) // Expected ≈1.0
        Assert.True(Math.Abs(height2 - 2.0) < 0.25) // Expected ≈2.0

module MeshingTests =

    [<Fact>]
    let ``generateMesh handles single voxel correctly`` () =
        let heights = Array2D.create 1 1 1.0
        let (topQuads, sideFaces) = Meshing.generateMesh heights

        Assert.Single(topQuads) |> ignore
        Assert.Equal(4, sideFaces.Length) // 4 sides for single voxel

        let (x, z, x2, z2, h) = topQuads.Head
        Assert.Equal(0, x)
        Assert.Equal(0, z)
        Assert.Equal(1, x2)
        Assert.Equal(1, z2)
        Assert.Equal(1, h)

    [<Fact>]
    let ``generateMesh merges contiguous top faces`` () =
        // 2x2 plateau at height 2
        let heights = Array2D.init 2 2 (fun _ _ -> 2.0)
        let (topQuads, _) = Meshing.generateMesh heights

        Assert.Single(topQuads) |> ignore
        let (x, z, x2, z2, h) = topQuads.Head
        Assert.Equal(0, x)
        Assert.Equal(0, z)
        Assert.Equal(2, x2)
        Assert.Equal(2, z2)
        Assert.Equal(2, h)

module ReconstructTests =

    [<Fact>]
    let ``Reconstruction maintains seed value`` () =
        let normals = Array2D.create 3 3 { Nx = 0.0; Ny = 0.0; Nz = 1.0 }
        let seed = (Point(1, 1), 5.0)

        let heightMap =
            Reconstruct.reconstructHeightFromNormals normals seed 0.05 10.0 10 1e-5

        Assert.Equal(5.0, heightMap.[1, 1], 2) // Maintain seed value with precision

    [<Fact>]
    let ``Reconstruction handles invalid normals gracefully`` () =
        let normals =
            Array2D.init 2 2 (fun y x ->
                if x = 0 && y = 0 then
                    { Nx = 0.0; Ny = 0.0; Nz = 0.0 } // Invalid normal
                else
                    { Nx = 0.0; Ny = 0.0; Nz = 1.0 })

        let heightMap =
            Reconstruct.reconstructHeightFromNormals normals (Point(1, 1), 0.0) 0.1 5.0 20 1e-4

        // Should not throw and produce reasonable values
        Assert.False(Double.IsNaN(heightMap.[0, 0]))
        Assert.Equal(0.0, heightMap.[1, 1])

module VisualizationTests =



    [<Fact>]
    let ``Visualization handles NaN values gracefully`` () =
        let tempFile = Path.GetTempFileName() + ".png"

        let heights =
            Array2D.init 5 5 (fun y x -> if y = 2 && x = 2 then Double.NaN else 100.0)

        Visualization.saveHeightMapAsImage heights tempFile

        use img = Image.Load<L8>(tempFile)
        Assert.Equal(0uy, img.[2, 2].PackedValue) // NaN becomes black

        File.Delete(tempFile)
