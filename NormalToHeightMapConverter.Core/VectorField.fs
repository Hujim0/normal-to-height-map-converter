namespace NormalToHeightMapConverter

module VectorField =
    open NormalToHeightMapConverter.PointTypes
    open OpenCvSharp
    open System

    let rotateFloatMatrix (matrix: float[,]) (angleDegrees: float) : float[,] =
        let height = matrix.GetLength(0)
        let width = matrix.GetLength(1)

        use src = new Mat(height, width, MatType.CV_64FC1)

        // Fill the matrix with float values
        for y = 0 to height - 1 do
            for x = 0 to width - 1 do
                src.Set<double>(y, x, matrix.[y, x])

        let center = new Point2f(float32 (width - 1) / 2.0f, float32 (height - 1) / 2.0f)
        let rotationMatrix = Cv2.GetRotationMatrix2D(center, -angleDegrees, 1.0)

        // Calculate the bounding rectangle of the rotated image
        let corners =
            [| new Point2f(0.0f, 0.0f)
               new Point2f(float32 width, 0.0f)
               new Point2f(float32 width, float32 height)
               new Point2f(0.0f, float32 height) |]

        let rotatedCorners =
            corners
            |> Array.map (fun p ->
                let x =
                    rotationMatrix.Get<double>(0, 0) * float p.X
                    + rotationMatrix.Get<double>(0, 1) * float p.Y
                    + rotationMatrix.Get<double>(0, 2)

                let y =
                    rotationMatrix.Get<double>(1, 0) * float p.X
                    + rotationMatrix.Get<double>(1, 1) * float p.Y
                    + rotationMatrix.Get<double>(1, 2)

                new Point2f(float32 x, float32 y))

        // Find bounding rectangle of rotated corners
        let minX = rotatedCorners |> Array.minBy (fun p -> p.X) |> (fun p -> p.X)
        let maxX = rotatedCorners |> Array.maxBy (fun p -> p.X) |> (fun p -> p.X)
        let minY = rotatedCorners |> Array.minBy (fun p -> p.Y) |> (fun p -> p.Y)
        let maxY = rotatedCorners |> Array.maxBy (fun p -> p.Y) |> (fun p -> p.Y)

        let boundingRect =
            new Rect(
                int (floor (float minX)),
                int (floor (float minY)),
                int (ceil (float (maxX - minX))),
                int (ceil (float (maxY - minY)))
            )

        // Adjust rotation matrix to account for translation
        rotationMatrix.Set(0, 2, rotationMatrix.Get<double>(0, 2) - float boundingRect.X)
        rotationMatrix.Set(1, 2, rotationMatrix.Get<double>(1, 2) - float boundingRect.Y)

        use dst = new Mat(boundingRect.Height, boundingRect.Width, MatType.CV_64FC1)

        Cv2.WarpAffine(
            src,
            dst,
            rotationMatrix,
            new Size(boundingRect.Width, boundingRect.Height),
            InterpolationFlags.Linear,
            BorderTypes.Constant,
            Scalar.All(0.0)
        )

        // Convert back to F# array
        let result = Array2D.zeroCreate<float> boundingRect.Height boundingRect.Width

        for y = 0 to boundingRect.Height - 1 do
            for x = 0 to boundingRect.Width - 1 do
                result.[y, x] <- dst.Get<double>(y, x)

        result

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
