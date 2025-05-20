namespace FSharpMosaicApi.ImageProcessors

open SkiaSharp

module ColorHelper =
    let getAvgColor(bitmap: SKBitmap, pixelPositions: SKPointI array) =
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

    let packColor0BGR8888(color: UnpackedColor) =
        (int32 color.Blue <<< 16)
        ||| (int32 color.Green <<< 8)
        ||| (int32 color.Red)

    let unpackColor0BGR8888(packedColor: int32) =
        let r = packedColor &&& 0xFF
        let g = (packedColor >>> 8) &&& 0xFF
        let b = (packedColor >>> 16) &&& 0xFF
        UnpackedColor(r, g, b)

    let toHexString(color: UnpackedColor) =
        $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
