namespace NormalToHeightMapConverter

module Meshing =
    open System
    open System.IO

    let generateMesh (heights: float[,]) =
        let w = Array2D.length1 heights
        let d = Array2D.length2 heights

        let hmap =
            Array2D.init w d (fun x z ->
                let h = heights.[x, z]
                if h < 0.5 then 0 else ceil h |> int)

        // Generate top quads using greedy meshing with immutable set tracking
        let rec processGrid z x visitedSet quads =
            if z >= d then
                (visitedSet, quads)
            elif x >= w then
                processGrid (z + 1) 0 visitedSet quads
            else
                let h = hmap.[x, z]

                if h <= 0 || Set.contains (x, z) visitedSet then
                    processGrid z (x + 1) visitedSet quads
                else
                    // Grow in +X direction
                    let rec findX curX =
                        if curX < w && hmap.[curX, z] = h && not (Set.contains (curX, z) visitedSet) then
                            findX (curX + 1)
                        else
                            curX

                    let x2 = findX (x + 1)

                    // Grow in +Z direction
                    let rec findZ curZ =
                        if curZ < d then
                            let rowValid =
                                [ x .. x2 - 1 ]
                                |> List.forall (fun xx ->
                                    hmap.[xx, curZ] = h && not (Set.contains (xx, curZ) visitedSet))

                            if rowValid then findZ (curZ + 1) else curZ
                        else
                            curZ

                    let z2 = findZ (z + 1)

                    // Create visited set for this rectangle
                    let rectPoints =
                        [ for zz in z .. z2 - 1 do
                              for xx in x .. x2 - 1 -> (xx, zz) ]
                        |> Set.ofList

                    let newVisited = Set.union visitedSet rectPoints
                    let newQuads = (x, z, x2, z2, h) :: quads
                    processGrid z x2 newVisited newQuads

        let (_, topQuads) = processGrid 0 0 Set.empty []
        let topQuads = List.rev topQuads

        // Generate side faces using list comprehensions
        let sideFaces =
            [ for z in 0 .. d - 1 do
                  for x in 0 .. w - 1 do
                      let h = hmap.[x, z]

                      if h > 0 then
                          // East face (+X)
                          let hEast = if x + 1 < w then hmap.[x + 1, z] else 0

                          if h > hEast then
                              let yBot, yTop = float hEast, float h
                              let v0 = (float (x + 1), yBot, float z)
                              let v1 = (float (x + 1), yBot, float (z + 1))
                              let v2 = (float (x + 1), yTop, float (z + 1))
                              let v3 = (float (x + 1), yTop, float z)
                              yield (v0, v3, v2, v1)

                          // West face (-X)
                          let hWest = if x - 1 >= 0 then hmap.[x - 1, z] else 0

                          if h > hWest then
                              let yBot, yTop = float hWest, float h
                              let v0 = (float x, yBot, float z)
                              let v1 = (float x, yBot, float (z + 1))
                              let v2 = (float x, yTop, float (z + 1))
                              let v3 = (float x, yTop, float z)
                              yield (v0, v1, v2, v3)

                          // North face (+Z)
                          let hNorth = if z + 1 < d then hmap.[x, z + 1] else 0

                          if h > hNorth then
                              let yBot, yTop = float hNorth, float h
                              let v0 = (float x, yBot, float (z + 1))
                              let v1 = (float (x + 1), yBot, float (z + 1))
                              let v2 = (float (x + 1), yTop, float (z + 1))
                              let v3 = (float x, yTop, float (z + 1))
                              yield (v0, v1, v2, v3)

                          // South face (-Z)
                          let hSouth = if z - 1 >= 0 then hmap.[x, z - 1] else 0

                          if h > hSouth then
                              let yBot, yTop = float hSouth, float h
                              let v0 = (float x, yBot, float z)
                              let v1 = (float (x + 1), yBot, float z)
                              let v2 = (float (x + 1), yTop, float z)
                              let v3 = (float x, yTop, float z)
                              yield (v0, v3, v2, v1) ]

        (topQuads, sideFaces)

    let fst3 (a, _, _) = a
    let snd3 (_, b, _) = b
    let thd3 (_, _, c) = c

    let writeObj (filename: string) (topQuads, sideFaces) =
        use writer = new StreamWriter(filename)
        writer.WriteLine("# Generated by F# Greedy Mesher with side faces")
        writer.WriteLine()

        // Precompute all vertices
        let topVertices =
            topQuads
            |> List.collect (fun (x, z, x2, z2, h) ->
                let y = float h
                let v0 = (float x, y, float z)
                let v1 = (float x2, y, float z)
                let v2 = (float x2, y, float z2)
                let v3 = (float x, y, float z2)
                [ v0; v1; v2; v3 ])

        let sideVertices =
            sideFaces |> List.collect (fun (v0, v1, v2, v3) -> [ v0; v1; v2; v3 ])

        let allVertices = topVertices @ sideVertices

        // Write vertices
        for v in allVertices do
            writer.WriteLine $"v {fst3 v} {snd3 v} {thd3 v}"

        // Write top quad faces
        let writeQuadFaces baseIndex =
            writer.WriteLine $"f {baseIndex} {baseIndex + 1} {baseIndex + 2}"
            writer.WriteLine $"f {baseIndex} {baseIndex + 2} {baseIndex + 3}"

        let numTopQuads = List.length topQuads

        for i in 0 .. numTopQuads - 1 do
            let baseIndex = i * 4 + 1
            writeQuadFaces baseIndex

        // Write side face faces
        let sideBaseIndex = numTopQuads * 4 + 1
        let numSideFaces = List.length sideFaces

        for i in 0 .. numSideFaces - 1 do
            let baseIndex = sideBaseIndex + i * 4
            writeQuadFaces baseIndex

        let totalFaces = (numTopQuads + numSideFaces) * 2

        printfn
            $"OBJ written to {filename} with {numTopQuads} top quads and {numSideFaces} side quads ({totalFaces} total faces)"
