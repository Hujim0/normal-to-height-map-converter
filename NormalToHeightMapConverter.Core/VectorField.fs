namespace NormalToHeightMapConverter

module VectorField =
    open MathNet.Numerics
    open MathNet.Numerics.LinearAlgebra
    open System

    let private createRotationMatrix (angleDegrees: float) (centerX: float) (centerY: float) =
        let angleRadians = -angleDegrees * Math.PI / 180.0
        let cosAngle = cos angleRadians
        let sinAngle = sin angleRadians

        // Create 3x3 affine transformation matrix for rotation around center
        let m = Matrix<float>.Build.Dense(3, 3)
        m.[0, 0] <- cosAngle
        m.[0, 1] <- -sinAngle
        m.[0, 2] <- centerX * (1.0 - cosAngle) + centerY * sinAngle
        m.[1, 0] <- sinAngle
        m.[1, 1] <- cosAngle
        m.[1, 2] <- centerY * (1.0 - cosAngle) - centerX * sinAngle
        m.[2, 2] <- 1.0
        m

    let private getRotatedBounds (width: float) (height: float) (rotationMatrix: Matrix<float>) =
        // Get corners of original image
        let corners =
            [ [| 0.0; 0.0; 1.0 |]
              [| width; 0.0; 1.0 |]
              [| width; height; 1.0 |]
              [| 0.0; height; 1.0 |] ]

        // Apply rotation to corners
        let rotatedCorners =
            corners
            |> List.map (fun corner ->
                let vec = Vector<float>.Build.DenseOfArray(corner)
                let result = rotationMatrix * vec
                (result.[0] / result.[2], result.[1] / result.[2]))

        // Find bounding box
        let minX = rotatedCorners |> List.minBy fst |> fst
        let maxX = rotatedCorners |> List.maxBy fst |> fst
        let minY = rotatedCorners |> List.minBy snd |> snd
        let maxY = rotatedCorners |> List.maxBy snd |> snd

        (floor minX, ceil maxX, floor minY, ceil maxY)

    let private applyRotation
        (matrix: float[,])
        (rotationMatrix: Matrix<float>)
        (bounds: float * float * float * float)
        =
        let height = matrix.GetLength(0)
        let width = matrix.GetLength(1)
        let (minX, maxX, minY, maxY) = bounds

        let newWidth = int (ceil (maxX - minX))
        let newHeight = int (ceil (maxY - minY))

        // Create result matrix
        let result = Array2D.zeroCreate<float> newHeight newWidth

        // Inverse rotation matrix for sampling
        let inverseRotation = rotationMatrix.Inverse()

        // Apply bilinear interpolation
        for y = 0 to newHeight - 1 do
            for x = 0 to newWidth - 1 do
                // Map from output coordinates to input coordinates
                let srcX = float x + minX
                let srcY = float y + minY

                // Apply inverse transformation
                let homogeneous = Vector<float>.Build.Dense([| srcX; srcY; 1.0 |])
                let transformed = inverseRotation * homogeneous

                let origX = transformed.[0] / transformed.[2]
                let origY = transformed.[1] / transformed.[2]

                // Bilinear interpolation
                let x0 = int (floor origX)
                let y0 = int (floor origY)
                let x1 = x0 + 1
                let y1 = y0 + 1

                let dx = origX - float x0
                let dy = origY - float y0

                // Check bounds and interpolate
                if x0 >= 0 && x1 < width && y0 >= 0 && y1 < height then
                    let v00 = matrix.[y0, x0]
                    let v01 = matrix.[y0, x1]
                    let v10 = matrix.[y1, x0]
                    let v11 = matrix.[y1, x1]

                    let top = v00 * (1.0 - dx) + v01 * dx
                    let bottom = v10 * (1.0 - dx) + v11 * dx
                    result.[y, x] <- top * (1.0 - dy) + bottom * dy
                else
                    result.[y, x] <- 0.0

        result

    let rotateFloatMatrix (matrix: float[,]) (angleDegrees: float) : float[,] =
        let height = matrix.GetLength(0)
        let width = matrix.GetLength(1)

        // Calculate center
        let centerX = float (width - 1) / 2.0
        let centerY = float (height - 1) / 2.0

        // Create rotation matrix
        let rotationMatrix = createRotationMatrix angleDegrees centerX centerY

        // Get bounds of rotated image
        let bounds = getRotatedBounds (float width) (float height) rotationMatrix

        // Apply rotation with bilinear interpolation
        applyRotation matrix rotationMatrix bounds

    let rotateNormalVectorField (normals: NormalVector[,]) (angleDegrees: float) : NormalVector[,] =
        let height = normals.GetLength(0)
        let width = normals.GetLength(1)

        // Extract separate channels
        let nxArray = Array2D.init height width (fun y x -> normals.[y, x].Nx)
        let nyArray = Array2D.init height width (fun y x -> normals.[y, x].Ny)
        let nzArray = Array2D.init height width (fun y x -> normals.[y, x].Nz)
        let alphaArray = Array2D.init height width (fun y x -> normals.[y, x].Alpha)

        // Rotate each channel separately
        let rotatedNx = rotateFloatMatrix nxArray angleDegrees
        let rotatedNy = rotateFloatMatrix nyArray angleDegrees
        let rotatedNz = rotateFloatMatrix nzArray angleDegrees
        let rotatedAlpha = rotateFloatMatrix alphaArray angleDegrees

        // Recombine into NormalVector array
        let newHeight = rotatedNx.GetLength(0)
        let newWidth = rotatedNx.GetLength(1)

        Array2D.init newHeight newWidth (fun y x ->
            { Nx = rotatedNx.[y, x]
              Ny = rotatedNy.[y, x]
              Nz = rotatedNz.[y, x]
              Alpha = rotatedAlpha.[y, x] })

    let rotateVectorFieldNormals (normals: NormalVector[,]) (angleDegrees: float) : NormalVector[,] =
        let height = normals.GetLength(0)
        let width = normals.GetLength(1)
        let angleRadians = angleDegrees * Math.PI / 180.0
        let cosAngle = cos angleRadians
        let sinAngle = sin angleRadians

        Array2D.init height width (fun y x ->
            let n = normals.[y, x]
            let nx' = n.Nx * cosAngle - n.Ny * sinAngle
            let ny' = n.Nx * sinAngle + n.Ny * cosAngle

            { Nx = nx'
              Ny = ny'
              Nz = n.Nz
              Alpha = n.Alpha })

    let centeredCrop (image: 'T[,]) (targetHeight: int) (targetWidth: int) : 'T[,] =
        let height = image.GetLength(0)
        let width = image.GetLength(1)

        let startY = (height - targetHeight) / 2
        let startX = (width - targetWidth) / 2

        Array2D.init targetHeight targetWidth (fun y x ->
            let srcY = startY + y
            let srcX = startX + x

            if srcY >= 0 && srcY < height && srcX >= 0 && srcX < width then
                image.[srcY, srcX]
            else
                Unchecked.defaultof<'T>)
