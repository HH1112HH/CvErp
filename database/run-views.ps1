param(
    [string]$Server = '.\SQLEXPRESS',
    [string]$Database = 'CvErp',
    [string]$SqlFile = (Join-Path $PSScriptRoot 'views\00_create_views.sql')
)

# Chạy script view bằng .NET SqlClient để giữ nguyên literal tiếng Việt (UTF-8).
# Không dùng `sqlcmd -i` mặc định vì sqlcmd trên Windows PowerShell dễ đọc sai UTF-8,
# làm hỏng các chuỗi như "Vượt định mức" / "Tiết kiệm" trong view.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

$sql = [IO.File]::ReadAllText((Resolve-Path $SqlFile), [Text.Encoding]::UTF8)
$connectionString = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()

$batches = [regex]::Split($sql, '(?im)^\s*GO\s*$')
$count = 0
foreach ($batch in $batches) {
    if ($batch.Trim().Length -eq 0) { continue }
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $batch
    $cmd.CommandTimeout = 120
    [void]$cmd.ExecuteNonQuery()
    $count++
}
$conn.Close()
Write-Output "Đã chạy $count batch từ $SqlFile"
