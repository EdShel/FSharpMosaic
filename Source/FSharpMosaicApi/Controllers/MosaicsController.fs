namespace FSharpMosaicApi.Controllers

open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open SkiaSharp
open System
open System.Threading.Tasks
open System.IO
open System.IO.Compression
open System.Threading
open System.Text.Json
open FSharpMosaicApi.DataAccess
open System.ComponentModel.DataAnnotations

[<CLIMutable>]
type CreateMosaicModel = {
    [<Required>] SourceImage: IFormFile
    [<Required>] Density: Nullable<int>
}

[<CLIMutable>]
type ImportImagesModel = {
    ZipFilePath: string
}

type UnpackedColor =
    struct
        val Red: int32
        val Green: int32
        val Blue: int32

        new(r: int32, g: int32, b: int32) = {
            Red = r
            Green = g
            Blue = b
        }
    end
    

module ImagesHelper =
    let GetAvgColor(bitmap: SKBitmap, pixelPositions: SKPointI array) =
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
        UnpackedColor(int32 avgR, int32 avgG, int32 avgB)

    let PackColor0BGR8888(color: UnpackedColor) =
        (int32 color.Blue <<< 16)
        ||| (int32 color.Green <<< 8)
        ||| (int32 color.Red)

    let UnpackColor0BGR8888(packedColor: int32) =
        let r = packedColor &&& 0xFF
        let g = (packedColor >>> 8) &&& 0xFF
        let b = (packedColor >>> 16) &&& 0xFF
        UnpackedColor(r, g, b)

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
            let chunkSize = largestDimension / body.Density.Value
            let imagesX = bitmap.Width / chunkSize
            let imagesY = bitmap.Height / chunkSize
            let sourceImageSize = SKPointI(bitmap.Width, bitmap.Height)
            let paddedSourceImageSize = SKPointI(imagesX * chunkSize, imagesY * chunkSize)
            let padding = SKPointI(
                (sourceImageSize.X - paddedSourceImageSize.X) / 2,
                (sourceImageSize.Y - paddedSourceImageSize.Y) / 2)

            let chunkPositions =
                [| 0..chunkSize * chunkSize - 1 |]
                |> Array.map (fun i -> SKPointI(i % chunkSize, i / chunkSize))
            let batchesCount = imagesX * imagesY

            let chunksAvgColors : UnpackedColor array = Array.zeroCreate batchesCount
            let _ = Parallel.For(0, chunksAvgColors.Length, fun i ->
                let chunkTopLeftPosition = SKPointI(
                    (i % imagesX) * chunkSize,
                    (i / imagesX) * chunkSize) + padding
                let pixelPositions =
                    chunkPositions |> Array.map(fun p -> chunkTopLeftPosition + p)

                chunksAvgColors[i] <- ImagesHelper.GetAvgColor(bitmap, pixelPositions)
            )

            let precomputedHashes =
                ImageHashRepository.GetAllHashes()
                |> List.map(fun (id, hash) -> (id, ImagesHelper.UnpackColor0BGR8888(hash)))

            let chunkImagesIds : int array = Array.zeroCreate chunksAvgColors.Length
            let _ = Parallel.For(0, chunkImagesIds.Length, fun i ->
                let euclideanDistanceRgb (a: UnpackedColor) (b: UnpackedColor) =
                    let rDif = a.Red - b.Red
                    let gDif = a.Green - b.Green
                    let bDif = a.Blue - b.Blue
                    (rDif * rDif) + (gDif * gDif) + (bDif * bDif)

                let chunkColor = chunksAvgColors[i]
                let distanceToChunkAvgColor = euclideanDistanceRgb chunkColor

                let (closestImageId, _) =
                    precomputedHashes
                    |> List.minBy(fun (_, color) -> distanceToChunkAvgColor(color))
                chunkImagesIds[i] <- closestImageId
            )

            let piecesFileNames = ImageHashRepository.GetFileNames(chunkImagesIds)

            let targetImageResolution = 8192.0
            let resultImageScale =
                Math.Min(
                    targetImageResolution / float paddedSourceImageSize.X,
                    targetImageResolution / float paddedSourceImageSize.Y)
                |> fun v -> Math.Max(1.0, v)
                |> Math.Floor
                |> int
            let mosaicPieceSize = resultImageScale * chunkSize
            let resultBitmap = new SKBitmap(
                paddedSourceImageSize.X * resultImageScale,
                paddedSourceImageSize.Y * resultImageScale,
                SKColorType.Rgba8888,
                SKAlphaType.Premul)
            use canvas = new SKCanvas(resultBitmap)
            for x = 0 to imagesX - 1 do
                for y = 0 to imagesY - 1 do
                    let rectBounds = SKRect(
                        float32 (x * mosaicPieceSize),
                        float32 (y * mosaicPieceSize),
                        float32 (x * mosaicPieceSize + mosaicPieceSize),
                        float32 (y * mosaicPieceSize + mosaicPieceSize))

                    let chunkId = y * imagesX + x
                    let chunkImageId = chunkImagesIds[chunkId]
                    let (_, imageName) = piecesFileNames |> List.find(fun (id, _) -> id = chunkImageId)
                    use imageStream = ImageFileRepository.OpenFile(imageName)
                    use imageBitmap = SKBitmap.Decode(imageStream)
                    canvas.DrawBitmap(imageBitmap, rectBounds)

            canvas.Flush()

            let resultSKData = resultBitmap.Encode(SKEncodedImageFormat.Png, 70)
            let resultStream = resultSKData.AsStream()
            this.File(resultStream, "image/png")

        match validatationResult with
        | Ok _ -> generateMosaic()
        | Error errors -> this.BadRequest(errors)

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

        let processZipEntry (entry: ZipArchiveEntry, pngFileName: string) =
            use entryStream = entry.Open()
            use bitmap = SKBitmap.Decode(entryStream)
            let bitmapCroppedSize = Math.Min(bitmap.Width, bitmap.Height)
            let padding = SKPointI(
                (bitmap.Width - bitmapCroppedSize) / 2,
                (bitmap.Height - bitmapCroppedSize) / 2)
                
            let allPixelsPositions =
                [| 0..bitmapCroppedSize * bitmapCroppedSize - 1 |]
                |> Array.map (fun i -> SKPointI(i % bitmap.Width, i / bitmap.Width) + padding)

            let imageAvgColor =
                ImagesHelper.GetAvgColor(bitmap, allPixelsPositions)
                |> ImagesHelper.PackColor0BGR8888

            use croppedBitmap = new SKBitmap(
                bitmapCroppedSize,
                bitmapCroppedSize,
                SKColorType.Rgba8888,
                SKAlphaType.Premul)
            let isCropOK = bitmap.ExtractSubset(
                croppedBitmap,
                SKRectI(padding.X, padding.Y, bitmap.Width - padding.X, bitmap.Height - padding.Y))
            if not isCropOK then
                raise(invalidOp("Unable to crop a bitmap"))

            use croppedStream = croppedBitmap.Encode(SKEncodedImageFormat.Png, 70).AsStream()
            ImageFileRepository.SaveFile(pngFileName, croppedStream)

            ImageHashRepository.Insert(imageAvgColor, pngFileName)

            raiseSSEJson({| FileName = entry.FullName; AvgColor = $"#%X{imageAvgColor}" |})

        let importFile (zipFilePath: string) =
            use zipStream = File.OpenRead(zipFilePath)
            use zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen = false)
            let entriesCount = zip.Entries.Count
            for i = 0 to entriesCount - 1 do
                raiseSSEJson({| Current = i; Total = entriesCount |})

                let entry = zip.Entries[i]
                let pngFileName = Path.ChangeExtension(entry.FullName, ".png")
                if not(ImageFileRepository.Exists(pngFileName)) then
                    processZipEntry(entry, pngFileName)
                    
        match validate body.ZipFilePath with
        | Ok filePath -> importFile(filePath)
        | Error errors -> writeErrors(errors)
