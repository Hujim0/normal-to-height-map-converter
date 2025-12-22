namespace NormalToHeightMapConverter

open NormalToHeightMapConverter.Reconstruct
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open System
open System.Collections.Generic

module Integration =

    let private preprocessNormals (normals: NormalVector[,]) : Normal[,] =
        let height = normals.GetLength(0)
        let width = normals.GetLength(1)

        Array2D.init height width (fun y x ->
            let n = normals.[y, x]

            if n.Alpha < 0.5 then
                { Nx = 0.0; Ny = 0.0; Nz = 1.0 }
            else
                let r = n.Nx
                let g = n.Ny
                let b = n.Nz

                let mag_original = sqrt (r * r + g * g + b * b)
                let diff = abs (mag_original - 1.0)

                let nx_norm, ny_norm, nz_norm =
                    if mag_original > 1e-6 then
                        r / mag_original, g / mag_original, b / mag_original
                    else
                        0.0, 0.0, 1.0

                let weight =
                    if diff < 0.02 then 1.0
                    elif diff < 0.3 then (0.3 - diff) / 0.28
                    else 0.0

                let blended_nx = weight * nx_norm
                let blended_ny = weight * ny_norm
                let blended_nz = weight * nz_norm + (1.0 - weight) * 1.0

                let mag_blended =
                    sqrt (blended_nx * blended_nx + blended_ny * blended_ny + blended_nz * blended_nz)

                if mag_blended < 1e-6 then
                    { Nx = 0.0; Ny = 0.0; Nz = 1.0 }
                else
                    { Nx = blended_nx / mag_blended
                      Ny = blended_ny / mag_blended
                      Nz = blended_nz / mag_blended })

    let private combineHeightMaps (method: string) (heightMaps: float[,][]) : float[,] =
        let validMethod =
            match method.ToLower() with
            | "min" -> "min"
            | "average" -> "average"
            | _ -> "average"

        let height = heightMaps.[0].GetLength(0)
        let width = heightMaps.[0].GetLength(1)

        Array2D.init height width (fun y x ->
            let values =
                heightMaps
                |> Array.map (fun hm -> hm.[y, x])
                |> Array.filter (fun v -> not (Double.IsNaN v))

            if values.Length = 0 then
                0.0
            else
                match validMethod with
                | "min" -> Array.min values
                | _ -> Array.average values)

    let calculateMinIterations (width: int) (height: int) (seedCount: int) : int =
        if seedCount = 1 then
            int (sqrt (float (width * width + height * height))) + 10
        else
            let maxDimension = max width height
            max 20 (min 2000 (maxDimension)) + 10

    let private generateBorderSeeds (width: int) (height: int) (count: int) : Point[] =
        if width <= 2 && height <= 2 then
            [| for y in 0 .. height - 1 do
                   for x in 0 .. width - 1 do
                       yield Point(x, y) |]
            |> Array.truncate (max 1 count)
        else
            let totalBorderPoints = 2 * (width + height) - 4
            let actualCount = min count totalBorderPoints

            let topEdge = [| for x in 0 .. width - 1 -> Point(x, 0) |]
            let rightEdge = [| for y in 1 .. height - 2 -> Point(width - 1, y) |]
            let bottomEdge = [| for x in width - 1 .. -1 .. 0 -> Point(x, height - 1) |]
            let leftEdge = [| for y in height - 2 .. -1 .. 1 -> Point(0, y) |]
            let borderPoints = Array.concat [ topEdge; rightEdge; bottomEdge; leftEdge ]

            let step = float totalBorderPoints / float actualCount

            let initialIndices =
                [| for i in 0 .. actualCount - 1 do
                       int (round (float i * step)) % totalBorderPoints |]
                |> Array.distinct

            let initialSet = Set.ofArray initialIndices
            let remaining = actualCount - initialIndices.Length

            let extraIndices =
                if remaining <= 0 then
                    [||]
                else
                    [| 0 .. totalBorderPoints - 1 |]
                    |> Array.filter (fun i -> not (initialSet.Contains i))
                    |> Array.truncate remaining

            let allIndices = Array.append initialIndices extraIndices
            allIndices |> Array.map (fun idx -> borderPoints.[idx])

    let integrateUsingReconstruction
        (normals: NormalVector[,])
        (eta0: float option)
        (tau: float option)
        (maxIter: int option)
        (eps: float option)
        (borderSeedCount: int option)
        (combineMethod: string option)
        : float[,] =

        let height = normals.GetLength(0)
        let width = normals.GetLength(1)

        let borderSeedCount = defaultArg borderSeedCount 4
        let eta0 = defaultArg eta0 0.05
        let tau = defaultArg tau 100.0
        let eps = defaultArg eps 1e-5
        let combineMethod = defaultArg combineMethod "average"

        let maxIter =
            match maxIter with
            | Some v -> v
            | None -> calculateMinIterations width height borderSeedCount

        printfn $"Image dimensions: {width}x{height}, border seeds: {borderSeedCount}, iterations: {maxIter}"
        printfn $"Combination method: {combineMethod}"

        let processedNormals = preprocessNormals normals

        let seedPoints = generateBorderSeeds width height borderSeedCount

        printfn $"Generated {seedPoints.Length} seed points at positions:"
        seedPoints |> Array.iter (fun p -> printfn $"  ({p.X}, {p.Y})")

        let heightMaps =
            Array.Parallel.map
                (fun seedPoint -> reconstructHeightFromNormals processedNormals (seedPoint, 0.0) eta0 tau maxIter eps)
                seedPoints

        combineHeightMaps combineMethod heightMaps

    let estimateHeightMap
        (normalMap: Image<Rgba32>)
        (eta0: float option)
        (tau: float option)
        (maxIter: int option)
        (eps: float option)
        (borderSeedCount: int option)
        (combineMethod: string option)
        : float[,] =

        let height = normalMap.Height
        let width = normalMap.Width

        let normals =
            Array2D.init height width (fun y x ->
                let pixel = normalMap.[x, y]

                let r = (float pixel.R / 255.0 * 2.0) - 1.0
                let g = (float pixel.G / 255.0 * 2.0) - 1.0
                let b = (float pixel.B / 255.0 * 2.0) - 1.0
                let alpha = float pixel.A / 255.0

                { Nx = r
                  Ny = g
                  Nz = b
                  Alpha = alpha })

        printfn $"Done!"
        integrateUsingReconstruction normals eta0 tau maxIter eps borderSeedCount combineMethod
