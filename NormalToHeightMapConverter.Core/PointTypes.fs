namespace NormalToHeightMapConverter

module PointTypes =
    type NormalVector =
        { Nx: float
          Ny: float
          Nz: float
          Alpha: float }

    type Point = { X: int; Y: int }
