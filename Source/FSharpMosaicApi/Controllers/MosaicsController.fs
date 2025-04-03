namespace FSharpMosaicApi.Controllers

open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open SkiaSharp
open System.Threading.Tasks
open System.IO.Compression
open System.Threading
open System.Text.Json
open FSharpMosaicApi.Database
open System.IO

[<CLIMutable>]
type CreateMosaicModel = {
    Image: IFormFile
    ImagesX: int
    ImagesY: int
}

[<CLIMutable>]
type ImportImagesModel = {
    ZipFilePath: string
}

module ImagesHelper =
    let getAvgColor (bitmap: SKBitmap, pixelPositions: SKPointI array): SKColor =
        let colors = pixelPositions |> Array.map(fun p -> bitmap.GetPixel(p.X, p.Y))
        let (_, avgR, avgG, avgB) =
            ((0.0, 0.0, 0.0, 0.0), colors)
            ||> Array.fold(fun acc color ->
                let (count, r, g, b) = acc
                // Incremental averaging algorithm
                let count' = count + 1.0
                let r' = r + ((float color.Red - r) / count')
                let g' = g + ((float color.Green - g) / count')
                let b' = b + ((float color.Blue - b) / count')
                (count', r', g', b')
            )
        SKColor(byte avgR, byte avgG, byte avgB, 255uy)

    let packColorRGBA8888 (color: SKColor) =
        (int32 color.Red <<< 24)
        ||| (int32 color.Green <<< 16)
        ||| (int32 color.Blue <<< 8)
        ||| (int32 color.Alpha)

[<ApiController>]
[<Route("/api/v1/mosaics")>]
type MosaicsController(
    logger : ILogger<MosaicsController>) =
    inherit ControllerBase()

    [<HttpPost>]
    member this.CreateMosaic([<FromForm>] body: CreateMosaicModel) =
        use sourceImgStream = body.Image.OpenReadStream()
        use bitmap = SKBitmap.Decode(sourceImgStream)

        // TODO: validation for img size and chunks

        let batchWidth = bitmap.Width / body.ImagesX
        let batchHeight = bitmap.Height / body.ImagesY

        let chunkPositions =
            [| 0..batchWidth * batchHeight - 1 |]
            |> Array.map (fun i -> SKPointI(i % batchWidth, i / batchWidth))

        let batches = body.ImagesX * body.ImagesY
        let chunksAvgColors : SKColor array = Array.zeroCreate batches
        let _ = Parallel.For(0, batches, fun i ->
            let batchTopLeftPosition = SKPointI(
                (i % body.ImagesX) * batchWidth,
                (i / body.ImagesX) * batchHeight)
            let pixelPositions =
                chunkPositions |> Array.map(fun p -> batchTopLeftPosition + p)

            chunksAvgColors[i] <- ImagesHelper.getAvgColor(bitmap, pixelPositions)
        )

        let squareSize = 32
        let resultBitmap = new SKBitmap(
            squareSize * body.ImagesX,
            squareSize * bitmap.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul)
        use canvas = new SKCanvas(resultBitmap)
        for x = 0 to body.ImagesX - 1 do
            for y = 0 to body.ImagesY - 1 do
                let rectBounds = SKRect(
                    float32 (x * squareSize),
                    float32 (y * squareSize),
                    float32 (x * squareSize + squareSize),
                    float32 (y * squareSize + squareSize))

                let i = y * body.ImagesX + x
                let color = chunksAvgColors[i]
                use paint = new SKPaint(Color = color)
                canvas.DrawRect(rectBounds, paint)
        canvas.Flush()

        let resultSKData = resultBitmap.Encode(SKEncodedImageFormat.Png, 70)
        let resultStream = resultSKData.AsStream()
        this.File(resultStream, "image/png")

    [<HttpPost("import")>]
    member this.ImportImages(
        [<FromBody>] body: ImportImagesModel,
        cancellationToken: CancellationToken
    ) =
        let response = this.Response
        response.Headers.Add("Content-Type", "text/event-stream")

        let toJson value = JsonSerializer.Serialize(value, JsonSerializerOptions(JsonSerializerDefaults.Web))
        let raiseSSE json: unit = 
            response.WriteAsync(json, cancellationToken)
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> ignore
        let raiseSSEJson data = toJson data |> raiseSSE

        let validate (zipFilePath: string) =
            if not (File.Exists(zipFilePath)) then
                this.ModelState.AddModelError(nameof(body.ZipFilePath), "File does not exist")
            elif Path.GetExtension(zipFilePath).ToLower() <> ".zip" then
                this.ModelState.AddModelError(nameof(body.ZipFilePath), "File must have a .zip extension")
            elif FileInfo(zipFilePath).Length = 0L then
                this.ModelState.AddModelError(nameof(body.ZipFilePath), "File must not be empty")

            if this.ModelState.ErrorCount > 0 then
                Error this.ModelState
            else
                Ok zipFilePath

        let writeErrors (modelState: ModelBinding.ModelStateDictionary) =
            for er in modelState do
                raiseSSEJson({| Field = er.Key; Error = er.Value |})

        let importFile (zipFilePath: string) =
            use zipStream = File.OpenRead(zipFilePath)
            use zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen = false)
            let entriesCount = zip.Entries.Count
            for i = 0 to entriesCount - 1 do
                raiseSSEJson({| Current = i; Total = entriesCount |})

                let entry = zip.Entries[i]
                use entryStream = entry.Open()
                use bitmap = SKBitmap.Decode(entryStream)
                
                let allPixelsPositions =
                    [| 0..bitmap.Width * bitmap.Height - 1 |]
                    |> Array.map (fun i -> SKPointI(i % bitmap.Width, i / bitmap.Width))

                let imageAvgColor =
                    ImagesHelper.getAvgColor(bitmap, allPixelsPositions)
                    |> ImagesHelper.packColorRGBA8888

                ImageHashRepository.insert(imageAvgColor, entry.FullName)

                raiseSSEJson({| FileName = entry.FullName; AvgColor = $"#%X{imageAvgColor}" |})

        match validate body.ZipFilePath with
        | Ok filePath -> importFile(filePath)
        | Error errors -> writeErrors(errors)


//[<ApiController>]
//[<Route("[controller]")>]
//type WeatherForecast2Controller (logger : ILogger<WeatherForecast2Controller>) =
//    inherit ControllerBase()

//    let summaries =
//        [|
//            "Freezing"
//            "Bracing"
//            "Chilly"
//            "Cool"
//            "Mild"
//            "Warm"
//            "Balmy"
//            "Hot"
//            "Sweltering"
//            "Scorching"
//        |]

//    [<HttpPost>]
//    member _.CreateMosaic([<FromForm>] body: CreateMosaicModel) =
//        printfn "Passed %A" (body)
//        Ok()
//    [<HttpGet>]
//    member _.Get() =
//        printfn "GETTTTT"
//        Ok("Hello")


    //[<HttpGet>]
    //member _.Get() =
    //    let rng = System.Random()
    //    rng.Next()