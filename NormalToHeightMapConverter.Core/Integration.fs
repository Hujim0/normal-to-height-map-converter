namespace NormalToHeightMapConverter

open NormalToHeightMapConverter.Reconstruct
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open System

module Integration =
    let private preprocessNormals (normals: NormalVector[,]) : Normal[,] =
        let height = normals.GetLength(0)
        let width = normals.GetLength(1)

        Array2D.init height width (fun y x ->
            let n = normals.[y, x]

            if n.Alpha < 0.5 then
                { Nx = 0.0; Ny = 0.0; Nz = 1.0 }
            else
                // Ensure normals are normalized
                let mag = sqrt (n.Nx * n.Nx + n.Ny * n.Ny + n.Nz * n.Nz)
                let nx = if mag > 1e-6 then n.Nx / mag else 0.0
                let ny = if mag > 1e-6 then n.Ny / mag else 0.0
                let nz = if mag > 1e-6 then n.Nz / mag else 1.0
                { Nx = nx; Ny = ny; Nz = nz })

    let private combineHeightMaps (heightMaps: float[,][]) : float[,] =
        let sampleCount = heightMaps.Length
        let height = heightMaps.[0].GetLength(0)
        let width = heightMaps.[0].GetLength(1)

        Array2D.init height width (fun y x ->
            let values =
                heightMaps
                |> Array.map (fun hm -> hm.[y, x])
                |> Array.filter (fun v -> not (Double.IsNaN v))

            if values.Length = 0 then 0.0 else Array.average values)

    /// Calculate minimum iterations needed to ensure all pixels are reached from corner seeds
    let private calculateMinIterations (width: int) (height: int) : int =
        // The maximum Manhattan distance from any corner to the center point
        // This ensures propagation reaches every pixel in the image
        let horizontalDistance = (width - 1) / 2
        let verticalDistance = (height - 1) / 2
        let minIterations = horizontalDistance + verticalDistance

        // Add buffer for stabilization (20% more iterations)
        let withBuffer = int (float minIterations * 1.2)

        // Set reasonable bounds
        max 10 (min 2000 withBuffer)

    let integrateUsingReconstruction
        (normals: NormalVector[,])
        (eta0: float option)
        (tau: float option)
        (maxIter: int option)
        (eps: float option)
        : float[,] =

        let height = normals.GetLength(0)
        let width = normals.GetLength(1)

        // Calculate optimal iterations if not provided
        let maxIter =
            match maxIter with
            | Some v -> v
            | None -> calculateMinIterations width height

        let eta0 = defaultArg eta0 0.05
        let tau = defaultArg tau 100.0
        let eps = defaultArg eps 1e-5

        // Preprocess normals (handle alpha and normalization)
        let processedNormals = preprocessNormals normals

        // Define four corner seeds
        let seeds =
            [| { X = 0; Y = 0 } // Top-left
               { X = width - 1; Y = 0 } // Top-right
               { X = 0; Y = height - 1 } // Bottom-left
               { X = width - 1; Y = height - 1 } |] // Bottom-right

        printfn $"Image dimensions: {width}x{height}, using {maxIter} iterations"

        // Run reconstruction from each seed in parallel
        let heightMaps =
            Array.Parallel.map
                (fun seedPoint -> reconstructHeightFromNormals processedNormals (seedPoint, 0.0) eta0 tau maxIter eps)
                seeds

        // Combine results by averaging non-NaN values
        combineHeightMaps heightMaps

    let estimateHeightMap
        (normalMap: Image<Rgba32>)
        (eta0: float option)
        (tau: float option)
        (maxIter: int option)
        (eps: float option)
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

                // Normalize the vector
                let mag = sqrt (r * r + g * g + b * b)
                let nx = if mag > 1e-6 then r / mag else 0.0
                let ny = if mag > 1e-6 then g / mag else 0.0
                let nz = if mag > 1e-6 then b / mag else 1.0

                { Nx = nx
                  Ny = ny
                  Nz = nz
                  Alpha = alpha })

        // Perform reconstruction integration with automatic iteration count
        integrateUsingReconstruction normals eta0 tau maxIter eps
