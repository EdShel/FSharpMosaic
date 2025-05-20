namespace FSharpMosaicApi.Controllers

open System
open System.ComponentModel.DataAnnotations
open Microsoft.AspNetCore.Http

[<AutoOpen>]
module MosaicsControllerModels =
    [<CLIMutable>]
    type CreateMosaicModel = {
        [<Required>] SourceImage: IFormFile
        [<Required>] Density: Nullable<int>
        [<Range(256, 4096)>] ResultImageSize: int
    }

    [<CLIMutable>]
    type ImportImagesModel = {
        ZipFilePath: string
    }
