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

                // Normalize the original vector if possible
                let nx_norm, ny_norm, nz_norm =
                    if mag_original > 1e-6 then
                        r / mag_original, g / mag_original, b / mag_original
                    else
                        0.0, 0.0, 1.0

                // Calculate weight based on deviation from unit length
                let weight =
                    if diff < 0.02 then 1.0
                    elif diff < 0.3 then (0.3 - diff) / 0.28 // Linear drop from 1.0 to 0.0 between 0.02 and 0.3
                    else 0.0

                // Blend normalized vector with default upward normal (0,0,1)
                let blended_nx = weight * nx_norm
                let blended_ny = weight * ny_norm
                let blended_nz = weight * nz_norm + (1.0 - weight) * 1.0

                // Normalize the blended vector
                let mag_blended =
                    sqrt (blended_nx * blended_nx + blended_ny * blended_ny + blended_nz * blended_nz)

                if mag_blended < 1e-6 then
                    { Nx = 0.0; Ny = 0.0; Nz = 1.0 }
                else
                    { Nx = blended_nx / mag_blended
                      Ny = blended_ny / mag_blended
                      Nz = blended_nz / mag_blended })

    /// Combine multiple height maps using specified method
    let private combineHeightMaps (method: string) (heightMaps: float[,][]) : float[,] =
        let validMethod =
            match method.ToLower() with
            | "min" -> "min"
            | "average" -> "average"
            | _ -> "average" // Default to average for unknown methods

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
                | _ -> Array.average values) // Default to average

    /// Calculate minimum iterations needed to ensure all pixels are reached from border seeds
    let calculateMinIterations (width: int) (height: int) (seedCount: int) : int =
        // For single seed, use diagonal distance
        if seedCount = 1 then
            int (sqrt (float (width * width + height * height))) + 10
        else
            // For multiple border seeds, use half the smaller dimension plus buffer
            let maxDimension = max width height
            max 20 (min 2000 (maxDimension)) + 10

    /// Generate equally spaced seed points around the image border
    let private generateBorderSeeds (width: int) (height: int) (count: int) : Point[] =
        // Handle tiny images
        if width <= 2 && height <= 2 then
            [| for y in 0 .. height - 1 do
                   for x in 0 .. width - 1 do
                       yield Point(x, y) |]
            |> Array.truncate (max 1 count)
        else
            // Calculate total border positions (without duplicate corners)
            let totalBorderPoints = 2 * (width + height) - 4
            let actualCount = min count totalBorderPoints

            // Create ordered list of border points in clockwise order
            let borderPoints = ResizeArray<Point>(totalBorderPoints)

            // Top edge (left to right)
            for x = 0 to width - 1 do
                borderPoints.Add(Point(x, 0))

            // Right edge (top to bottom, excluding corners)
            for y = 1 to height - 2 do
                borderPoints.Add(Point(width - 1, y))

            // Bottom edge (right to left)
            for x = width - 1 downto 0 do
                borderPoints.Add(Point(x, height - 1))

            // Left edge (bottom to top, excluding corners)
            for y = height - 2 downto 1 do
                borderPoints.Add(Point(0, y))

            // Calculate equally spaced indices
            let step = float totalBorderPoints / float actualCount

            let seedIndices =
                [| for i in 0 .. actualCount - 1 do
                       int (round (float i * step)) % totalBorderPoints |]
                |> Array.distinct // Remove potential duplicates from rounding

            // Create seed points array
            let mutable seeds = Array.zeroCreate<Point> seedIndices.Length

            for i = 0 to seedIndices.Length - 1 do
                seeds.[i] <- borderPoints.[seedIndices.[i]]

            // Ensure we have exactly the requested count (pad if needed)
            if seeds.Length < actualCount then
                let extraNeeded = actualCount - seeds.Length

                let extraSeeds =
                    [| for i in 0 .. extraNeeded - 1 do
                           borderPoints.[(i * totalBorderPoints / extraNeeded) % totalBorderPoints] |]

                Array.append seeds extraSeeds
            else
                seeds

    let integrateUsingReconstruction
        (normals: NormalVector[,])
        (eta0: float option)
        (tau: float option)
        (maxIter: int option)
        (eps: float option)
        (borderSeedCount: int option)
        (combineMethod: string option) // New parameter for combination method
        : float[,] =

        let height = normals.GetLength(0)
        let width = normals.GetLength(1)

        // Set default parameters
        let borderSeedCount = defaultArg borderSeedCount 4
        let eta0 = defaultArg eta0 0.05
        let tau = defaultArg tau 100.0
        let eps = defaultArg eps 1e-5
        let combineMethod = defaultArg combineMethod "average" // Default combination method

        // Automatically determine iterations if not specified
        let maxIter =
            match maxIter with
            | Some v -> v
            | None -> calculateMinIterations width height borderSeedCount

        printfn $"Image dimensions: {width}x{height}, border seeds: {borderSeedCount}, iterations: {maxIter}"
        printfn $"Combination method: {combineMethod}"

        // Preprocess normals
        let processedNormals = preprocessNormals normals

        // Generate seed positions around border
        let seedPoints = generateBorderSeeds width height borderSeedCount

        printfn $"Generated {seedPoints.Length} seed points at positions:"
        seedPoints |> Array.iter (fun p -> printfn $"  ({p.X}, {p.Y})")

        // Run reconstruction from each seed in parallel
        let heightMaps =
            Array.Parallel.map
                (fun seedPoint -> reconstructHeightFromNormals processedNormals (seedPoint, 0.0) eta0 tau maxIter eps)
                seedPoints

        // Combine results using specified method
        combineHeightMaps combineMethod heightMaps

    let estimateHeightMap
        (normalMap: Image<Rgba32>)
        (eta0: float option)
        (tau: float option)
        (maxIter: int option)
        (eps: float option)
        (borderSeedCount: int option)
        (combineMethod: string option) // New parameter
        : float[,] =

        let height = normalMap.Height
        let width = normalMap.Width

        // Convert image to normal vectors
        let normals =
            Array2D.init height width (fun y x ->
                let pixel = normalMap.[x, y] // ImageSharp uses [x,y] indexing

                // Convert RGBA to normalized normals
                let r = (float pixel.R / 255.0 * 2.0) - 1.0
                let g = (float pixel.G / 255.0 * 2.0) - 1.0
                let b = (float pixel.B / 255.0 * 2.0) - 1.0
                let alpha = float pixel.A / 255.0

                { Nx = r
                  Ny = g
                  Nz = b
                  Alpha = alpha })

        printfn $"Done!"
        // Perform reconstruction integration
        integrateUsingReconstruction normals eta0 tau maxIter eps borderSeedCount combineMethod
