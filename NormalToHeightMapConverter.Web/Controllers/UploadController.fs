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
open Microsoft.Extensions.FileProviders
open Microsoft.Extensions.Hosting
open Microsoft.Net.Http.Headers
open Microsoft.AspNetCore.Http.Features

module FileHelpers =
    let validateNormalMap (filePath: string) : bool =
        // TODO: Implement actual validation logic
        true

    let generate3DModel (inputPath: string) (outputDir: string) : unit =
        // TODO: Implement actual generation logic
        let dummyContent = "Dummy 3D model content"
        File.WriteAllText(Path.Combine(outputDir, "model.obj"), dummyContent)
        File.WriteAllText(Path.Combine(outputDir, "model.mtl"), dummyContent)
        File.WriteAllBytes(Path.Combine(outputDir, "height_map.png"), [| 137uy; 80uy; 78uy; 71uy |])
        File.WriteAllBytes(Path.Combine(outputDir, "preview.png"), [| 137uy; 80uy; 78uy; 71uy |])

    let sanitizeFileName (fileName: string) : string =
        Path.GetFileName(fileName)
        |> Seq.filter (fun c -> Char.IsLetterOrDigit(c) || c = '.' || c = '_' || c = '-' || c = ' ')
        |> Seq.truncate 255
        |> Seq.toArray
        |> System.String

    let computeSha256 (stream: Stream) : string =
        use sha256 = SHA256.Create()
        stream.Seek(0L, SeekOrigin.Begin) |> ignore
        let hashBytes = sha256.ComputeHash(stream)
        BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant()

    let getFileType (fileName: string) : string =
        let ext = Path.GetExtension(fileName).ToLowerInvariant()

        match ext with
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
type UploadController(uploadPath: string) =
    inherit ControllerBase()

    [<HttpPost("upload-normal")>]
    member this.UploadNormalMap() : Task<IActionResult> =
        task {
            if not this.Request.HasFormContentType then
                return this.BadRequest("Missing form data") :> IActionResult
            else
                let! form = this.Request.ReadFormAsync()

                match form.Files |> Seq.tryHead with
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

                            let hashDir = Path.Combine(uploadPath, fileHash)
                            Directory.CreateDirectory(hashDir) |> ignore

                            let safeName = FileHelpers.sanitizeFileName f.FileName
                            let ext = Path.GetExtension(safeName)
                            let normalMapPath = Path.Combine(hashDir, $"normal_map{ext}")
                            File.Move(tempFile, normalMapPath)

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
                                FileHelpers.generate3DModel normalMapPath hashDir
                                return this.Ok({| hash = fileHash |}) :> IActionResult
                        finally
                            if File.Exists(tempFile) then
                                File.Delete(tempFile)
        }

    [<HttpGet("upload-list/{hash}")>]
    member this.GetUploadList(hash: string) : Task<IActionResult> =
        task {
            let normalizedHash = hash.ToLowerInvariant()

            if
                normalizedHash.Length <> 64
                || not (Regex.IsMatch(normalizedHash, "^[a-f0-9]{64}$"))
            then
                return this.BadRequest("Invalid hash format") :> IActionResult
            else
                let dirPath = Path.Combine(uploadPath, normalizedHash)

                if not (Directory.Exists(dirPath)) then
                    return this.NotFound({| error = $"Directory with hash {hash} not found" |}) :> IActionResult
                else
                    let files =
                        Directory.GetFiles(dirPath)
                        |> Array.map (fun filePath ->
                            let fileInfo = FileInfo(filePath)
                            let fileName = fileInfo.Name
                            let fileType = FileHelpers.getFileType fileName

                            // Safe URL construction with explicit parts
                            let scheme = this.Request.Scheme
                            let host = this.Request.Host.Value
                            let escapedHash = Uri.EscapeDataString(normalizedHash)
                            let baseUrl = $"{scheme}://{host}/upload/{escapedHash}"

                            let escapedFileName = Uri.EscapeDataString(fileName)
                            let fileUrl = $"{baseUrl}/{escapedFileName}"

                            let previewUrl =
                                if fileType = "model" then
                                    let previewName = Uri.EscapeDataString("preview.png")
                                    Some $"{baseUrl}/{previewName}"
                                else
                                    None

                            {| filename = fileName
                               size = fileInfo.Length
                               last_modified = fileInfo.LastWriteTimeUtc.ToString("o")
                               ``type`` = fileType
                               url = fileUrl
                               preview_url = previewUrl |})

                    return this.Ok(files) :> IActionResult
        }
