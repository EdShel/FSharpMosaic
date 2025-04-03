namespace FSharpMosaicApi.Database

open Microsoft.Data.Sqlite

module ImageHashRepository =
    let createTableIfNotExists() =
        use connection = new SqliteConnection("Data Source=images.db")
        connection.Open()

        let command = connection.CreateCommand()
        command.CommandText <- "
            CREATE TABLE IF NOT EXISTS ImageHash (
                Id INTEGER PRIMARY KEY,
                Hash INTEGER NOT NULL,
                FileName TEXT NOT NULL
            )
        "
        command.ExecuteNonQuery()

    let insert(hash: int32, fileName: string) =
        use connection = new SqliteConnection("Data Source=images.db")
        connection.Open()

        let command = connection.CreateCommand()
        command.CommandText <- "
            INSERT INTO ImageHash (Hash, FileName)
            VALUES ($hash, $fileName);
        "
        command.Parameters.AddWithValue("$hash", hash) |> ignore
        command.Parameters.AddWithValue("$fileName", fileName) |> ignore

        let createdRows = command.ExecuteNonQuery()
        if createdRows <> 1 then
            raise(invalidOp("Didn't create the record"))