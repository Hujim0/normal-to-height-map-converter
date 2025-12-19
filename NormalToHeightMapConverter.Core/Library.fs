namespace NormalToHeightMapConverter.Core

open System
open System.Collections.Generic

module Reconstruct =
    type Normal = { Nx: float; Ny: float; Nz: float }
    type Point = { X: int; Y: int } // X = column, Y = row

    type private FoldState = { Frontier: Queue<Point>; Iter: int }

    let reconstructHeightFromNormals
        (normals: Normal array2d) // [row, col]
        (seed: Point * float)
        (eta0: float)
        (tau: float)
        (maxIter: int)
        (eps: float)
        : float array2d =

        let H = normals.GetLength(0) // Rows
        let W = normals.GetLength(1) // Columns

        // Initialize height map [row, col]
        let z = Array2D.create H W Double.NaN
        let (seedPt, seedZ) = seed
        z.[seedPt.Y, seedPt.X] <- seedZ // [row, col] indexing

        // Track known cells [row, col]
        let known = Array2D.create H W false
        known.[seedPt.Y, seedPt.X] <- true

        // Directions: (dx, dy, axis) where:
        // dx = column delta, dy = row delta
        let directions =
            [| (1, 0, 'x') // Right (positive column)
               (-1, 0, 'x') // Left
               (0, 1, 'y') // Down (positive row - image coordinates!)
               (0, -1, 'y') |] // Up

        let inline predFromNormal (n: Normal) (axis: char) : float option =
            if abs n.Nz < eps then
                None
            else
                match axis with
                | 'x' -> Some(-n.Nx / n.Nz) // Column direction
                | 'y' -> Some(-n.Ny / n.Nz) // Row direction
                | _ -> None

        let initialState =
            { Frontier =
                let q = Queue<Point>()
                q.Enqueue(seedPt)
                q
              Iter = 0 }

        let folder state _ =
            if state.Frontier.Count = 0 || state.Iter >= maxIter then
                state
            else
                let nextFrontier = Queue<Point>()
                let updates = Dictionary<Point, ResizeArray<float>>()

                while state.Frontier.Count > 0 do
                    let p = state.Frontier.Dequeue()
                    // CORRECT INDEXING: [row=Y, col=X]
                    let n = normals.[p.Y, p.X]

                    for (dx, dy, axis) in directions do
                        let nx = p.X + dx // New column
                        let ny = p.Y + dy // New row

                        // CORRECT BOUNDS CHECKING:
                        if nx >= 0 && nx < W && ny >= 0 && ny < H && not known.[ny, nx] then
                            match predFromNormal n axis with
                            | Some dz ->
                                // Current height at [row, col] = [p.Y, p.X]
                                let currentHeight = z.[p.Y, p.X]
                                let newZ = currentHeight + dz
                                let neighbor = { X = nx; Y = ny }

                                match updates.TryGetValue(neighbor) with
                                | (true, list) -> list.Add(newZ)
                                | false, _ ->
                                    let newList = ResizeArray<float>()
                                    newList.Add(newZ)
                                    updates.Add(neighbor, newList)
                            | None -> ()

                let eta = eta0 / (1.0 + float state.Iter / tau)

                for kvp in updates do
                    let pt = kvp.Key
                    let preds = kvp.Value
                    let avgZ = preds.ToArray() |> Array.average
                    let current = z.[pt.Y, pt.X] // [row, col]

                    let finalZ =
                        if Double.IsNaN(current) then
                            avgZ
                        else
                            (1.0 - eta) * current + eta * avgZ

                    z.[pt.Y, pt.X] <- finalZ // [row=Y, col=X]
                    known.[pt.Y, pt.X] <- true
                    nextFrontier.Enqueue(pt)

                { Frontier = nextFrontier
                  Iter = state.Iter + 1 }

        Seq.init maxIter id |> Seq.fold folder initialState |> ignore // We only care about side effects on z

        z
