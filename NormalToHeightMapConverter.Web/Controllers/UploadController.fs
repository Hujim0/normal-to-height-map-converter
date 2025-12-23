namespace NormalToHeightMapConverter.Web

open System
open System.IO
open System.Security.Cryptography
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent
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

    // Static semaphore dictionary for hash-based locking
    static let hashSemaphores = ConcurrentDictionary<string, SemaphoreSlim>()

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
                        let mutable fileHash = ""
                        let mutable result = None

                        try
                            use tempStream = new FileStream(tempFile, FileMode.Create)
                            do! f.CopyToAsync(tempStream)

                            use stream = File.OpenRead(tempFile)
                            fileHash <- FileHelpers.computeSha256 stream

                            let uploadPath = getUploadPath ()
                            let hashDir = Path.Combine(uploadPath, fileHash)

                            let semaphore =
                                match hashSemaphores.TryGetValue(fileHash) with
                                | true, s -> s
                                | false, _ ->
                                    let newSem = new SemaphoreSlim(1, 1)
                                    let existing = hashSemaphores.GetOrAdd(fileHash, newSem)

                                    if Object.ReferenceEquals(existing, newSem) then
                                        newSem
                                    else
                                        newSem.Dispose()
                                        existing

                            try
                                do! semaphore.WaitAsync() |> Async.AwaitTask

                                if Directory.Exists(hashDir) then
                                    result <- Some(this.Ok({| hash = fileHash |}) :> IActionResult)
                                else
                                    Directory.CreateDirectory(hashDir) |> ignore

                                    let safeName = FileHelpers.sanitizeFileName f.FileName
                                    let ext = Path.GetExtension(safeName)
                                    let normalMapPath = Path.Combine(hashDir, $"normal_map{ext}")

                                    File.Move(tempFile, normalMapPath)
                                    FileHelpers.setFilePermissionsForWeb (normalMapPath)

                                    if not (FileHelpers.validateNormalMap normalMapPath) then
                                        Directory.Delete(hashDir, true)

                                        result <-
                                            Some(
                                                this.StatusCode(
                                                    422,
                                                    {| error = "Uploaded image is not a valid normal map"
                                                       details =
                                                        [ "Blue channel values outside expected range"
                                                          "Missing neutral gray baseline" ] |}
                                                )
                                                :> IActionResult
                                            )
                                    else
                                        let generationSettings =
                                            { Eta0 = settings.HeightMap.Eta0
                                              Tau = settings.HeightMap.Tau
                                              Epsilon = settings.HeightMap.Epsilon
                                              Seeds = settings.HeightMap.Seeds
                                              Combine = settings.HeightMap.Combine }

                                        heightMapService.GenerateFromPath(normalMapPath, hashDir, generationSettings)
                                        result <- Some(this.Ok({| hash = fileHash |}) :> IActionResult)
                            finally
                                semaphore.Release() |> ignore
                        finally
                            if File.Exists(tempFile) then
                                File.Delete(tempFile)

                        match result with
                        | Some r -> return r
                        | None -> return this.StatusCode(500, "Internal server error") :> IActionResult
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

                            let baseUrl = $"/uploads/{Uri.EscapeDataString(normalizedHash)}"

                            let fileUrl = $"{baseUrl}/{Uri.EscapeDataString(fileName)}"

                            {| filename = fileName
                               size = fileInfo.Length
                               last_modified = fileInfo.LastWriteTimeUtc.ToString("o")
                               ``type`` = fileType
                               url = fileUrl |})

                    return this.Ok(files) :> IActionResult
        }
