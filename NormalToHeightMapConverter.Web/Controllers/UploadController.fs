namespace NormalToHeightMapConverter.Web

open System
open System.IO
open System.Security.Cryptography
open System.Text.RegularExpressions
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Http.Features
open NormalToHeightMapConverter.Web.Services

module FileHelpers =
    open System.Runtime.InteropServices
    open NormalToHeightMapConverter.Meshing

    let validateNormalMap (_: string) : bool = true

    let setFilePermissionsForWeb (filePath: string) =
        try
            // Only apply on Linux systems
            if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
                let mode =
                    UnixFileMode.UserRead
                    ||| UnixFileMode.UserWrite
                    ||| UnixFileMode.GroupRead
                    ||| UnixFileMode.OtherRead

                File.SetUnixFileMode(filePath, mode)
        with
        | :? PlatformNotSupportedException -> ()
        | _ -> ()


    let sanitizeFileName (fileName: string) =
        let clean =
            Path.GetFileNameWithoutExtension(fileName)
            |> Seq.filter (fun c -> Char.IsLetterOrDigit(c) || c = '_' || c = '-')
            |> Seq.truncate 255
            |> Seq.toArray
            |> string

        // Force valid extension (e.g., only .png/.jpg)
        let ext =
            match Path.GetExtension(fileName).ToLowerInvariant() with
            | ".png"
            | ".jpg"
            | ".jpeg" -> Path.GetExtension fileName
            | _ -> ".png"

        $"{clean}{ext}"

    let computeSha256 (stream: Stream) : string =
        use sha256 = SHA256.Create()
        stream.Seek(0L, SeekOrigin.Begin) |> ignore

        sha256.ComputeHash(stream)
        |> BitConverter.ToString
        |> fun s -> s.Replace("-", "").ToLowerInvariant()

    let getFileType (fileName: string) : string =
        Path.GetExtension(fileName).ToLowerInvariant()
        |> function
            | ".png"
            | ".jpg"
            | ".jpeg"
            | ".gif"
            | ".webp"
            | ".bmp"
            | ".tiff" -> "image"
            | ".obj"
            | ".mtl" -> "model"
            | _ -> "metadata"

[<ApiController>]
[<Route("api")>]
type UploadController(settings: AppSettings, heightMapService: IHeightMapService) =
    inherit ControllerBase()

    // Simple accessor using injected settings
    let getUploadPath () = settings.UploadPath

    [<HttpPost("upload-normal")>]
    member this.UploadNormalMap() : Task<IActionResult> =
        task {
            if not this.Request.HasFormContentType then
                return this.BadRequest("Missing form data") :> IActionResult
            else
                let! form = this.Request.ReadFormAsync()

                match Seq.tryHead form.Files with
                | None -> return this.BadRequest("No file uploaded") :> IActionResult
                | Some f ->
                    let contentType = f.ContentType.ToLowerInvariant()

                    let validTypes =
                        [ "image/jpeg"
                          "image/png"
                          "image/gif"
                          "image/webp"
                          "image/bmp"
                          "image/tiff" ]

                    if not (List.contains contentType validTypes) then
                        return
                            this.StatusCode(415, {| error = "Unsupported file type. Please upload an image file." |})
                            :> IActionResult
                    else
                        let tempFile = Path.GetTempFileName()

                        try
                            use tempStream = new FileStream(tempFile, FileMode.Create)
                            do! f.CopyToAsync(tempStream)

                            use stream = File.OpenRead(tempFile)
                            let fileHash = FileHelpers.computeSha256 stream

                            let hashDir = Path.Combine(getUploadPath (), fileHash)
                            Directory.CreateDirectory(hashDir) |> ignore

                            let safeName = FileHelpers.sanitizeFileName f.FileName
                            let ext = Path.GetExtension(safeName)
                            let normalMapPath = Path.Combine(hashDir, $"normal_map{ext}")
                            File.Move(tempFile, normalMapPath)
                            FileHelpers.setFilePermissionsForWeb (normalMapPath)

                            if not (FileHelpers.validateNormalMap normalMapPath) then
                                Directory.Delete(hashDir, true)

                                return
                                    this.StatusCode(
                                        422,
                                        {| error = "Uploaded image is not a valid normal map"
                                           details =
                                            [ "Blue channel values outside expected range"
                                              "Missing neutral gray baseline" ] |}
                                    )
                                    :> IActionResult
                            else
                                // Create settings object using configured defaults
                                let generationSettings =
                                    { Eta0 = settings.HeightMap.Eta0
                                      Tau = settings.HeightMap.Tau
                                      Epsilon = settings.HeightMap.Epsilon
                                      Seeds = settings.HeightMap.Seeds
                                      Combine = settings.HeightMap.Combine }

                                heightMapService.GenerateFromPath(normalMapPath, hashDir, generationSettings)
                                return this.Ok({| hash = fileHash |}) :> IActionResult
                        finally
                            if File.Exists(tempFile) then
                                File.Delete(tempFile)
        }

    [<HttpGet("upload-list/{hash}")>]
    member this.GetUploadList(hash: string) : Task<IActionResult> =
        task {
            let normalizedHash = hash.ToLowerInvariant()
            let hashRegex = "^[a-f0-9]{64}$"

            if normalizedHash.Length <> 64 || not (Regex.IsMatch(normalizedHash, hashRegex)) then
                return this.BadRequest("Invalid hash format") :> IActionResult
            else
                let dirPath = Path.Combine(getUploadPath (), normalizedHash)

                if not (Directory.Exists(dirPath)) then
                    return this.NotFound({| error = $"Directory with hash {hash} not found" |}) :> IActionResult
                else
                    let files =
                        Directory.GetFiles(dirPath)
                        |> Array.map (fun filePath ->
                            let fileInfo = FileInfo(filePath)
                            let fileName = fileInfo.Name
                            let fileType = FileHelpers.getFileType fileName

                            let baseUrl =
                                // $"{this.Request.Scheme}://{this.Request.Host.Value}/uploads/{Uri.EscapeDataString(normalizedHash)}"
                                $"/uploads/{Uri.EscapeDataString(normalizedHash)}"

                            let fileUrl = $"{baseUrl}/{Uri.EscapeDataString(fileName)}"

                            {| filename = fileName
                               size = fileInfo.Length
                               last_modified = fileInfo.LastWriteTimeUtc.ToString("o")
                               ``type`` = fileType
                               url = fileUrl |})

                    return this.Ok(files) :> IActionResult
        }
