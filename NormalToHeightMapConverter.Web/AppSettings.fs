// AppSettings.fs
namespace NormalToHeightMapConverter.Web

type HeightMapConfig =
    { Eta0: float option
      Tau: float option
      Epsilon: float option
      Seeds: int option
      Combine: string }

type AppSettings =
    { UploadPath: string
      HeightMap: HeightMapConfig }
