namespace FSharpMosaicApi.DataAccess

open Microsoft.Data.Sqlite

module ImageHashRepository =
    let private openConnection() =
        let connection = new SqliteConnection("Data Source=images.db")
        connection.Open()
        connection

    let createTableIfNotExists() =
        use connection = openConnection()

        let command = connection.CreateCommand()
        command.CommandText <- "
            CREATE TABLE IF NOT EXISTS ImageHash (
                Id INTEGER PRIMARY KEY,
                Hash INTEGER NOT NULL
            )
        "
        command.ExecuteNonQuery()

    let insert(hash: int, fileName: string) =
        use connection = openConnection()

        let command = connection.CreateCommand()
        command.CommandText <- "
            INSERT INTO ImageHash (Hash, FileName)
            VALUES ($hash, $fileName);
        "
        command.Parameters.AddWithValue("$hash", hash) |> ignore
        command.Parameters.AddWithValue("$fileName", fileName) |> ignore

        let createdRows = command.ExecuteNonQuery()
        if createdRows <> 1 then
            invalidOp("Didn't create the record")

    let getAllHashes() =
        use connection = openConnection()

        let command = connection.CreateCommand()
        
        command.CommandText <- "SELECT Id, Hash FROM ImageHash"
        let reader = command.ExecuteReader()
        let idOrdinal = reader.GetOrdinal("Id")
        let hashOrdinal = reader.GetOrdinal("Hash")
        let result = [
            while reader.Read() do
                let id = reader.GetInt32(idOrdinal)
                let hash = reader.GetInt32(hashOrdinal)
                yield (id, hash)
        ]
        result

    let getFileNames(ids: int array) =
        use connection = openConnection()

        let command = connection.CreateCommand()
        
        let inClause =
            ids
            |> Array.distinct
            |> Array.map(fun x -> x.ToString())
            |> String.concat(",")
        command.CommandText <- $"SELECT Id, FileName FROM ImageHash WHERE Id IN ({inClause})"
        command.Parameters.AddWithValue("$ids", ids) |> ignore

        let reader = command.ExecuteReader()
        let idOrdinal = reader.GetOrdinal("Id")
        let fileNameOrdinal = reader.GetOrdinal("FileName")
        let result = [
            while reader.Read() do
                let id = reader.GetInt32(idOrdinal)
                let fileName = reader.GetString(fileNameOrdinal)
                yield (id, fileName)
        ]
        result

