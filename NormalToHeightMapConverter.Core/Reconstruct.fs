namespace NormalToHeightMapConverter


open System
open System.Collections.Generic

module Reconstruct =
    type Normal = { Nx: float; Ny: float; Nz: float }
    type Point = { X: int; Y: int }

    type private FoldState = { Frontier: Queue<Point>; Iter: int }

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

        let z = Array2D.create H W Double.NaN
        let (seedPt, seedZ) = seed
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
              Iter = 0 }

        let folder state _ =
            if state.Iter >= maxIter then
                state
            else
                let eta = eta0 / (1.0 + float state.Iter / tau)
                let nextFrontier = Queue<Point>()
                let updates = Dictionary<Point, ResizeArray<float>>()

                while state.Frontier.Count > 0 do
                    let p = state.Frontier.Dequeue()
                    let n = normals.[p.Y, p.X]

                    for (dx, dy) in directions do
                        let nx = p.X + dx
                        let ny = p.Y + dy

                        if nx >= 0 && nx < W && ny >= 0 && ny < H then
                            match predFromNormal n dx dy with
                            | Some dz ->
                                let currentHeight = z.[p.Y, p.X]

                                if not (Double.IsNaN(currentHeight)) then
                                    let newZ = currentHeight + dz
                                    let neighbor = { X = nx; Y = ny }

                                    if not (isFixedPoint neighbor) then
                                        match updates.TryGetValue(neighbor) with
                                        | (true, list) -> list.Add(newZ)
                                        | false, _ ->
                                            let newList = ResizeArray<float>()
                                            newList.Add(newZ)
                                            updates.Add(neighbor, newList)
                            | None -> ()

                // Apply updates
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

                    z.[pt.Y, pt.X] <- finalZ
                    nextFrontier.Enqueue(pt)

                { Frontier = nextFrontier
                  Iter = state.Iter + 1 }

        Seq.init maxIter id |> Seq.fold folder initialState |> ignore
        z
