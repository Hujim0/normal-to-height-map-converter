namespace NormalToHeightMapConverter

open NormalToHeightMapConverter.VectorField
open NormalToHeightMapConverter.PointTypes
open System
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing

module Integration =
    open MathNet.Numerics.Statistics

    type IntegrationMethod =
        | Sum
        | Trapezoid
        | Simpson

    let combineHeights (heights: float[,][]) : float[,] =
        let sampleCount = heights.Length

        if sampleCount = 0 then
            invalidArg "heights" "Array cannot be empty"

        let h = heights.[0].GetLength(0)
        let w = heights.[0].GetLength(1)
        let result = Array2D.zeroCreate<float> h w

        for y = 0 to h - 1 do
            for x = 0 to w - 1 do
                let mutable sum = 0.0

                for i = 0 to sampleCount - 1 do
                    sum <- sum + heights.[i].[y, x]

                result.[y, x] <- sum / float sampleCount

        result

    let private integrateSum (gradientField: float[,]) axis : float[,] =
        let h = gradientField.GetLength(0)
        let w = gradientField.GetLength(1)
        let result = Array2D.copy gradientField

        if axis = 1 then // Integrate along columns (x-direction)
            for y = 0 to h - 1 do
                for x = 1 to w - 1 do
                    result.[y, x] <- result.[y, x - 1] + gradientField.[y, x]
        else // Integrate along rows (y-direction)
            for x = 0 to w - 1 do
                for y = 1 to h - 1 do
                    result.[y, x] <- result.[y - 1, x] + gradientField.[y, x]

        result

    let private integrateTrapezoid (gradientField: float[,]) axis : float[,] =
        let h = gradientField.GetLength(0)
        let w = gradientField.GetLength(1)
        let result = Array2D.zeroCreate<float> h w

        if axis = 1 then // Integrate along columns (x-direction)
            for y = 0 to h - 1 do
                let mutable sum = 0.0

                for x = 0 to w - 1 do
                    if x = 0 then
                        sum <- gradientField.[y, x]
                    else
                        sum <- sum + 0.5 * (gradientField.[y, x] + gradientField.[y, x - 1])

                    result.[y, x] <- sum
        else // Integrate along rows (y-direction)
            for x = 0 to w - 1 do
                let mutable sum = 0.0

                for y = 0 to h - 1 do
                    if y = 0 then
                        sum <- gradientField.[y, x]
                    else
                        sum <- sum + 0.5 * (gradientField.[y, x] + gradientField.[y - 1, x])

                    result.[y, x] <- sum

        result

    let private integrateSimpson (gradientField: float[,]) axis : float[,] =
        let h = gradientField.GetLength(0)
        let w = gradientField.GetLength(1)
        let result = Array2D.zeroCreate<float> h w

        // Simpson's rule requires odd number of points, handle edge cases
        let simpsonRule (values: float[]) =
            let n = values.Length - 1

            if n < 2 then
                Array.sum values
            else
                let mutable sum = values.[0] + values.[n]
                // Handle even number of intervals by using trapezoid for the last one
                let endIndex = if n % 2 = 0 then n - 1 else n

                for i = 1 to endIndex - 1 do
                    if i % 2 = 0 then
                        sum <- sum + 2.0 * values.[i]
                    else
                        sum <- sum + 4.0 * values.[i]

                if n % 2 = 0 then
                    // Add trapezoid rule for the last interval
                    sum <- sum + 1.5 * values.[endIndex] + 0.5 * values.[n]
                    (sum * (1.0 / 3.0))
                else
                    (sum * (1.0 / 3.0))

        if axis = 1 then // Integrate along columns (x-direction)
            for y = 0 to h - 1 do
                let values = Array.zeroCreate<float> w

                for x = 0 to w - 1 do
                    values.[x] <- gradientField.[y, x]
                    let subValues = values.[0..x]
                    result.[y, x] <- simpsonRule subValues
        else // Integrate along rows (y-direction)
            for x = 0 to w - 1 do
                let values = Array.zeroCreate<float> h

                for y = 0 to h - 1 do
                    values.[y] <- gradientField.[y, x]
                    let subValues = values.[0..y]
                    result.[y, x] <- simpsonRule subValues

        result

    let integrateGradientField (gradientField: float[,]) (axis: int) (method': IntegrationMethod) : float[,] =
        match method' with
        | Sum -> integrateSum gradientField axis
        | Trapezoid -> integrateTrapezoid gradientField axis
        | Simpson -> integrateSimpson gradientField axis

    let calculateHeights
        (leftGradients: float[,])
        (topGradients: float[,])
        (integrationMethod: IntegrationMethod)
        : float[,] * float[,] * float[,] * float[,] =

        let leftHeights = integrateGradientField leftGradients 1 integrationMethod

        let height = leftGradients.GetLength(0)
        let width = leftGradients.GetLength(1)

        let flippedLeftGradients =
            Array2D.init height width (fun y x -> -leftGradients.[y, width - 1 - x])

        let flippedRightHeights =
            integrateGradientField flippedLeftGradients 1 integrationMethod

        let rightHeights =
            Array2D.init height width (fun y x -> flippedRightHeights.[y, width - 1 - x])

        let topHeights = integrateGradientField topGradients 0 integrationMethod

        let flippedTopGradients =
            Array2D.init height width (fun y x -> -topGradients.[height - 1 - y, x])

        let flippedBottomHeights =
            integrateGradientField flippedTopGradients 0 integrationMethod

        let bottomHeights =
            Array2D.init height width (fun y x -> flippedBottomHeights.[height - 1 - y, x])

        leftHeights, rightHeights, topHeights, bottomHeights

    let calculateConfidence (heights: float[,][]) : float[,] =
        let sampleCount = heights.Length

        if sampleCount = 0 then
            invalidArg "heights" "Array cannot be empty"

        let h = heights.[0].GetLength(0)
        let w = heights.[0].GetLength(1)
        let result = Array2D.zeroCreate<float> h w

        for y = 0 to h - 1 do
            for x = 0 to w - 1 do
                let values = Array.zeroCreate<float> sampleCount

                for i = 0 to sampleCount - 1 do
                    values.[i] <- heights.[i].[y, x]

                let stdDev =
                    if sampleCount > 1 then
                        Statistics.StandardDeviation(values)
                    else
                        0.0

                result.[y, x] <- -stdDev

        result

    let inline clip (value: float) (minValue: float) (maxValue: float) = max minValue (min maxValue value)

    let calculateGradients (normals: NormalVector[,]) : float[,] * float[,] =
        let height = normals.GetLength(0)
        let width = normals.GetLength(1)
        let leftGradients = Array2D.zeroCreate<float> height width
        let topGradients = Array2D.zeroCreate<float> height width

        for y = 0 to height - 1 do
            for x = 0 to width - 1 do
                let n = normals.[y, x]

                // Horizontal angle and gradient
                let horizontalAngle = acos (clip n.Nx -1.0 1.0)
                leftGradients.[y, x] <- sign ((horizontalAngle - Math.PI / 2.0) * (1.0 - sin horizontalAngle))

                // Vertical angle and gradient
                let verticalAngle = acos (clip n.Ny -1.0 1.0)
                topGradients.[y, x] <- -sign((verticalAngle - Math.PI / 2.0) * (1.0 - sin verticalAngle))

        leftGradients, topGradients

    let private processAngle
        (vectorField: NormalVector[,])
        (angle: float)
        (integrationMethod: IntegrationMethod)
        (originalShape: int * int)
        =

        let height, width = originalShape

        let rotatedFieldImage = rotateNormalVectorField vectorField angle
        let rotatedField = rotateVectorFieldNormals rotatedFieldImage angle

        let h = rotatedField.GetLength(0)
        let w = rotatedField.GetLength(1)

        for y = 0 to h - 1 do
            for x = 0 to w - 1 do
                let n = rotatedField.[y, x]

                if n.Alpha < 0.5 then
                    rotatedField.[y, x] <-
                        { n with
                            Nx = 0.0
                            Ny = 0.0
                            Nz = 1.0
                            Alpha = n.Alpha }

        let leftGradients, topGradients = calculateGradients rotatedField

        let leftHeights, rightHeights, topHeights, bottomHeights =
            calculateHeights leftGradients topGradients integrationMethod

        let combinedHeights =
            combineHeights [| leftHeights; rightHeights; topHeights; bottomHeights |]

        let unrotatedHeights = rotateFloatMatrix combinedHeights -angle

        centeredCrop unrotatedHeights height width

    let integrateVectorField
        (vectorField: NormalVector[,])
        (targetIterationCount: int)
        (integrationMethod: IntegrationMethod)
        : float[,] =

        let height = vectorField.GetLength(0)
        let width = vectorField.GetLength(1)
        let originalShape = (height, width)

        // Create angles from 0 to 90 degrees
        let angles =
            [| 0.0 .. (90.0 / float targetIterationCount) .. 90.0 |]
            |> Array.take targetIterationCount

        let results =
            angles
            |> Array.Parallel.map (fun angle -> processAngle vectorField angle integrationMethod originalShape)

        // Average the results
        combineHeights results

    let estimateHeightMap
        (normalMap: Image<Rgba32>)
        (targetIterationCount: int)
        (integrationMethod: IntegrationMethod)
        : float[,] =

        let height = normalMap.Height
        let width = normalMap.Width

        // Convert ImageSharp image to Normal array
        let normals =
            Array2D.init height width (fun y x ->
                let pixel = normalMap[x, y] // ImageSharp uses [x, y] indexing

                // Convert RGBA to normalized normal vector
                // Assuming the normal map stores normals in RGB channels (0-255) that need to be mapped to [-1, 1]
                let r = (float pixel.R / 255.0 * 2.0) - 1.0
                let g = (float pixel.G / 255.0 * 2.0) - 1.0
                let b = (float pixel.B / 255.0 * 2.0) - 1.0
                let alpha = float pixel.A / 255.0

                // Normalize the vector
                let magnitude = sqrt (r * r + g * g + b * b)
                let nx = if magnitude > 1e-6 then r / magnitude else 0.0
                let ny = if magnitude > 1e-6 then g / magnitude else 0.0
                let nz = if magnitude > 1e-6 then b / magnitude else 1.0

                { Nx = nx
                  Ny = ny
                  Nz = nz
                  Alpha = alpha })

        // Integrate the vector field
        integrateVectorField normals targetIterationCount integrationMethod
