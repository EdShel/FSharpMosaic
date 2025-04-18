namespace FSharpMosaicApi.DataAccess

open System.IO

module ImageFileRepository =
    let private baseDirectory = "./ImagesData"

    let SaveFile(fileName: string, data: Stream) =
        let filePath = Path.Combine(baseDirectory, fileName)
        let fileDir = Path.GetDirectoryName(filePath)
        Directory.CreateDirectory(fileDir) |> ignore
        use file = new FileStream(filePath, FileMode.Create, FileAccess.Write)
        data.CopyTo(file)

    let OpenFile(fileName: string) =
        let filePath = Path.Combine(baseDirectory, fileName)
        File.OpenRead(filePath)

    let Exists(fileName: string) =
        let filePath = Path.Combine(baseDirectory, fileName)
        File.Exists(filePath)
