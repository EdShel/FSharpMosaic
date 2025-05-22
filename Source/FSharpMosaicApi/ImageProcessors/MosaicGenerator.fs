namespace FSharpMosaicApi.ImageProcessors

open SkiaSharp
open System.Threading.Tasks
open FSharpMosaicApi.DataAccess
open System

module MosaicGenerator =
    let generateMosaicPng(bitmap: SKBitmap, density: int, resultImageSize: int) =
        let largestDimension = Math.Max(bitmap.Width, bitmap.Height)
        let chunkSize = largestDimension / density
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

            chunksAvgColors[i] <- ColorHelper.getAvgColor(bitmap, pixelPositions)
        )

        let precomputedHashes =
            ImageHashRepository.getAllHashes()
            |> List.map(fun (id, hash) -> (id, ColorHelper.unpackColor0BGR8888(hash)))

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

        let piecesFileNames = ImageHashRepository.getFileNames(chunkImagesIds)

        // Group chunks by their image - this minimizes disk read operations if image repeats
        let repeatedImages =
            chunkImagesIds
            |> Array.indexed
            |> Array.groupBy snd
            |> Array.map (fun (imageId, pairs) ->
                let imageFileName = piecesFileNames |> List.find (fun (id, _) -> id = imageId) |> snd
                let chunksIds = pairs |> Array.map fst |> Array.toList
                (imageFileName, chunksIds)
            )

        let targetImageResolution = float resultImageSize
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
        use paint = new SKPaint(IsAntialias = true)
        for (imageName, chunksIds) in repeatedImages do
            use imageStream = ImageFileRepository.openFile(imageName)
            use imageBitmap = SKBitmap.Decode(imageStream)
            for chunkId in chunksIds do
                let x = chunkId % imagesX
                let y = chunkId / imagesX
                let rectBounds = SKRect(
                    float32 (x * mosaicPieceSize),
                    float32 (y * mosaicPieceSize),
                    float32 (x * mosaicPieceSize + mosaicPieceSize),
                    float32 (y * mosaicPieceSize + mosaicPieceSize))
                canvas.DrawBitmap(imageBitmap, rectBounds, paint)

        canvas.Flush()

        let resultSKData = resultBitmap.Encode(SKEncodedImageFormat.Png, 100)
        resultSKData.AsStream()
