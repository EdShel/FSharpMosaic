namespace FSharpMosaicApi.ImageProcessors

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
