namespace FSharpMosaicApi.ImageProcessors

open FSharpMosaicApi.Networking
open FSharpMosaicApi.DataAccess
open Microsoft.AspNetCore.Http
open System
open System.IO
open System.IO.Compression
open System.Threading
open SkiaSharp

module ImagesZipFileImporter =
    let importZipFile(zipFilePath: string, response: HttpResponse, cancellationToken: CancellationToken) =
        async {
            let raiseEvent eventName data =
                SseHelper.raiseEvent(response, cancellationToken) eventName data

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

                // Re-encode image to have the same width & height:
                // this saves on disk space and we won't need to crop it during mosaic generation
                use croppedBitmap = new SKBitmap(
                    bitmapCroppedSize,
                    bitmapCroppedSize,
                    SKColorType.Rgba8888,
                    SKAlphaType.Premul)
                let isCropOK = bitmap.ExtractSubset(
                    croppedBitmap,
                    SKRectI(padding.X, padding.Y, bitmap.Width - padding.X, bitmap.Height - padding.Y))
                if not isCropOK then
                    invalidOp("Unable to crop a bitmap")

                use croppedStream = croppedBitmap.Encode(SKEncodedImageFormat.Png, 70).AsStream()
                ImageFileRepository.saveFile(pngFileName, croppedStream)

                let imageAvgColor = ColorHelper.getAvgColor(bitmap, allPixelsPositions)
                let encodedColor = ColorHelper.packColor0BGR8888(imageAvgColor)
                ImageHashRepository.insert(encodedColor, pngFileName)

                imageAvgColor
        
            use zipStream = File.OpenRead(zipFilePath)
            use zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen = false)
            let entriesCount = zip.Entries.Count
            for i = 0 to entriesCount - 1 do
                let entry = zip.Entries[i]
                let pngFileName = Path.ChangeExtension(entry.FullName, ".png")
                if not(ImageFileRepository.exists(pngFileName)) then
                    let color = processZipEntry(entry, pngFileName)
                    do! raiseEvent "progress" {| Current = i; Total = entriesCount; Color = ColorHelper.toHexString(color) |}

            do! raiseEvent "completed" {| Success = true |}
        }
