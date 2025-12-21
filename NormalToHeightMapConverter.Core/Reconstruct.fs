namespace NormalToHeightMapConverter

open System
open System.Collections.Generic
open SixLabors.ImageSharp

module Reconstruct =
    type Normal = { Nx: float; Ny: float; Nz: float }

    type private FoldState =
        { Frontier: Queue<Point>
          Iter: int
          CurrentNormals: Normal[,] }

    let reconstructHeightFromNormals
        (normals: Normal[,])
        (seed: Point * float)
        (eta0: float)
        (tau: float)
        (maxIter: int)
        (eps: float)
        : float[,] =

        let H = normals.GetLength(0)
        let W = normals.GetLength(1)

        // Create mutable copy of normals and adjust seed if needed
        let currentNormals = Array2D.init H W (fun y x -> normals.[y, x])
        let (seedPt, originalSeedZ) = seed

        let seedZ =
            if originalSeedZ < 0.0 then
                currentNormals.[seedPt.Y, seedPt.X] <- { Nx = 0.0; Ny = 0.0; Nz = 1.0 }
                0.0
            else
                originalSeedZ

        let z = Array2D.create H W Double.NaN
        z.[seedPt.Y, seedPt.X] <- seedZ

        let directions = [| (1, 0); (-1, 0); (0, 1); (0, -1) |]

        let inline predFromNormal (n: Normal) (dx: int) (dy: int) : float option =
            if abs n.Nz < eps then
                None
            else
                let step = if dx <> 0 then float dx else float dy
                let dz = if dx <> 0 then -n.Nx / n.Nz else -n.Ny / n.Nz
                Some(dz * step)

        let isFixedPoint (pt: Point) = pt.X = seedPt.X && pt.Y = seedPt.Y

        let initialState =
            { Frontier =
                let q = Queue<Point>()
                q.Enqueue(seedPt)
                q
              Iter = 0
              CurrentNormals = currentNormals }

        let folder state _ =
            if state.Iter >= maxIter then
                state
            else
                let eta = eta0 / (1.0 + float state.Iter / tau)
                let nextFrontier = Queue<Point>()
                let updates = Dictionary<Point, ResizeArray<float>>()

                if state.Iter % 50 = 0 then
                    printfn $"{state.Iter}"

                while state.Frontier.Count > 0 do
                    let p = state.Frontier.Dequeue()
                    let n = state.CurrentNormals.[p.Y, p.X] // Use current (potentially corrected) normals

                    for (dx, dy) in directions do
                        let nx = p.X + dx
                        let ny = p.Y + dy

                        if nx >= 0 && nx < W && ny >= 0 && ny < H then
                            match predFromNormal n dx dy with
                            | Some dz ->
                                let currentHeight = z.[p.Y, p.X]

                                if not (Double.IsNaN(currentHeight)) then
                                    let newZ = currentHeight + dz
                                    let neighbor = Point(nx, ny)

                                    if not (isFixedPoint neighbor) then
                                        match updates.TryGetValue(neighbor) with
                                        | (true, list) -> list.Add(newZ)
                                        | false, _ ->
                                            let newList = ResizeArray<float>()
                                            newList.Add(newZ)
                                            updates.Add(neighbor, newList)
                            | None -> ()

                // Apply updates and handle height clamping/normal reset
                for kvp in updates do
                    let pt = kvp.Key
                    let preds = kvp.Value
                    let avgZ = preds.ToArray() |> Array.average
                    let current = z.[pt.Y, pt.X]

                    let finalZ =
                        if Double.IsNaN(current) then
                            avgZ
                        else
                            (1.0 - eta) * current + eta * avgZ

                    // Clamp negative heights and reset normal
                    let finalZ, resetNeeded = if finalZ < 0.0 then (0.0, true) else (finalZ, false)

                    if resetNeeded then
                        state.CurrentNormals.[pt.Y, pt.X] <- { Nx = 0.0; Ny = 0.0; Nz = 1.0 }

                    z.[pt.Y, pt.X] <- finalZ
                    nextFrontier.Enqueue(pt)

                { Frontier = nextFrontier
                  Iter = state.Iter + 1
                  CurrentNormals = state.CurrentNormals } // Persist updated normals

        Seq.init maxIter id |> Seq.fold folder initialState |> ignore

        z
