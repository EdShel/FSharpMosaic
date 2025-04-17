namespace FSharpMosaicApi.DataAccess

open System.IO

module ImageFileRepository =
    let private baseDirectory = "./ImagesData"

    let saveFile(fileName: string, data: Stream) =
        let filePath = Path.Combine(baseDirectory, fileName)
        Directory.CreateDirectory(filePath) |> ignore
        use file = new FileStream(filePath, FileMode.Create, FileAccess.Write)
        data.CopyTo(file)

    let openFile(fileName: string) =
        let filePath = Path.Combine(baseDirectory, fileName)
        File.OpenRead(filePath)
