namespace FSharpMosaicApi.Controllers

open Microsoft.AspNetCore.Mvc
open SkiaSharp
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open FSharpMosaicApi.ImageProcessors
open FSharpMosaicApi.Networking

[<ApiController>]
[<Route("/api/v1/mosaics")>]
type MosaicsController() =
    inherit ControllerBase()

    [<HttpPost>]
    member this.CreateMosaic([<FromForm>] body: CreateMosaicModel): IActionResult =
        use sourceImgStream = body.SourceImage.OpenReadStream()
        use bitmap = SKBitmap.Decode(sourceImgStream)

        let largestDimension = Math.Max(bitmap.Width, bitmap.Height)

        let validatationResult =
            if bitmap.Width > 4096 || bitmap.Height > 4096 then
               this.ModelState.AddModelError(nameof(body.SourceImage), "Image is too large")
            if body.Density.Value > largestDimension then
                this.ModelState.AddModelError(nameof(body.Density), "Density is too big for this image")

            if this.ModelState.ErrorCount > 0 then
                Error this.ModelState
            else
                Ok true

        let generateMosaic() =
            let resultStream = MosaicGenerator.generateMosaicPng(bitmap, body.Density.Value, body.ResultImageSize)
            this.File(resultStream, "image/png")

        match validatationResult with
        | Ok _ -> generateMosaic()
        | Error errors -> this.BadRequest(errors)

    [<HttpGet("import")>]
    member this.ImportImages(
        [<FromQuery>] query: ImportImagesModel,
        cancellationToken: CancellationToken
    ): Task<IActionResult> =
        async {
            let response = this.Response
            SseHelper.setEventStreamHeader response

            let validate (zipFilePath: string) =
                if not (File.Exists(zipFilePath)) then
                    this.ModelState.AddModelError(nameof(query.ZipFilePath), "File does not exist")
                elif Path.GetExtension(zipFilePath).ToLower() <> ".zip" then
                    this.ModelState.AddModelError(nameof(query.ZipFilePath), "File must have a .zip extension")
                elif FileInfo(zipFilePath).Length = 0L then
                    this.ModelState.AddModelError(nameof(query.ZipFilePath), "File must not be empty")

                if this.ModelState.ErrorCount > 0 then
                    Error this.ModelState
                else
                    Ok zipFilePath
    
            match validate query.ZipFilePath with
            | Ok filePath ->
                do! ImagesZipFileImporter.importZipFile(filePath, response, cancellationToken)
            | Error errors ->
                do! SseHelper.raiseEvent(response, cancellationToken) "validation" {|
                    Errors = errors
                    |> Seq.map (fun entry -> (entry.Key, entry.Value.Errors |> Seq.map (fun errors -> errors.ErrorMessage)))
                    |> dict
                |}

            return EmptyResult() :> IActionResult
        } |> Async.StartAsTask
